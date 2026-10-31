using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Portfolio.Api.Services.Backup;

public record StoredObject(string Key, long Size, DateTime LastModifiedUtc);

/// <summary>Key/value blob storage for backups. Keys are relative to the configured prefix and use '/' separators.</summary>
public interface IObjectStore
{
    bool IsEnabled { get; }
    string Description { get; }
    Task<IReadOnlyList<StoredObject>> ListAsync(string prefix, CancellationToken ct);
    Task UploadAsync(string key, string localPath, string contentType, CancellationToken ct);
    /// <summary>Downloads to <paramref name="localPath"/>; returns false if the key does not exist.</summary>
    Task<bool> DownloadAsync(string key, string localPath, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}

public static class ObjectStoreFactory
{
    public static IObjectStore Create(BackupOptions o, string contentRoot)
    {
        if (!o.Enabled) return new DisabledObjectStore();
        return o.Provider.Equals("FileSystem", StringComparison.OrdinalIgnoreCase)
            ? new FileSystemObjectStore(Path.GetFullPath(Path.Combine(contentRoot, o.LocalPath!)), o.Prefix)
            : new S3ObjectStore(o);
    }
}

public sealed class DisabledObjectStore : IObjectStore
{
    public bool IsEnabled => false;
    public string Description => "disabled";
    private static InvalidOperationException Off() => new("Backup storage is not configured.");
    public Task<IReadOnlyList<StoredObject>> ListAsync(string prefix, CancellationToken ct) => throw Off();
    public Task UploadAsync(string key, string localPath, string contentType, CancellationToken ct) => throw Off();
    public Task<bool> DownloadAsync(string key, string localPath, CancellationToken ct) => throw Off();
    public Task DeleteAsync(string key, CancellationToken ct) => throw Off();
}

public sealed class S3ObjectStore : IObjectStore, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;
    private readonly string _prefix;

    public S3ObjectStore(BackupOptions o)
    {
        var config = new AmazonS3Config
        {
            ForcePathStyle = o.ForcePathStyle,
            // R2 and B2 do not support every checksum the SDK sends by default.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
        if (string.IsNullOrWhiteSpace(o.ServiceUrl)) config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(o.Region);
        else
        {
            config.ServiceURL = o.ServiceUrl;
            config.AuthenticationRegion = RegionFor(o.ServiceUrl, o.Region);
        }
        _client = new AmazonS3Client(new BasicAWSCredentials(o.AccessKeyId, o.SecretAccessKey), config);
        _bucket = o.Bucket!;
        _prefix = o.Prefix;
        Description = $"s3://{_bucket}/{_prefix}";
    }

    /// <summary>
    /// With Region "auto", takes the region from endpoints shaped like https://s3.&lt;region&gt;.example.com
    /// (Backblaze B2, Wasabi, AWS); other endpoints such as Cloudflare R2 keep "auto".
    /// </summary>
    public static string RegionFor(string serviceUrl, string region)
    {
        if (!region.Equals("auto", StringComparison.OrdinalIgnoreCase) || !Uri.TryCreate(serviceUrl, UriKind.Absolute, out var uri)) return region;
        var labels = uri.Host.Split('.');
        return labels.Length >= 4 && labels[0] == "s3" ? labels[1] : region;
    }

    public bool IsEnabled => true;
    public string Description { get; }

    public async Task<IReadOnlyList<StoredObject>> ListAsync(string prefix, CancellationToken ct)
    {
        var result = new List<StoredObject>();
        string? token = null;
        do
        {
            var response = await _client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = _bucket, Prefix = _prefix + prefix, ContinuationToken = token }, ct);
            foreach (var o in response.S3Objects ?? [])
                result.Add(new StoredObject(o.Key[_prefix.Length..], o.Size ?? 0, o.LastModified?.ToUniversalTime() ?? default));
            token = response.IsTruncated == true ? response.NextContinuationToken : null;
        } while (token is not null);
        return result;
    }

    public Task UploadAsync(string key, string localPath, string contentType, CancellationToken ct) =>
        _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket, Key = _prefix + key, FilePath = localPath, ContentType = contentType,
            UseChunkEncoding = false, // R2 rejects aws-chunked streaming uploads
        }, ct);

    public async Task<bool> DownloadAsync(string key, string localPath, CancellationToken ct)
    {
        try
        {
            using var response = await _client.GetObjectAsync(_bucket, _prefix + key, ct);
            await using var output = new FileStream(localPath, FileMode.Create, FileAccess.Write);
            await response.ResponseStream.CopyToAsync(output, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public Task DeleteAsync(string key, CancellationToken ct) => _client.DeleteObjectAsync(_bucket, _prefix + key, ct);

    public void Dispose() => _client.Dispose();
}

/// <summary>Stores objects as files under a folder. For local development and tests; it is not off-site.</summary>
public sealed class FileSystemObjectStore(string root, string prefix) : IObjectStore
{
    private readonly string _root = Path.Combine(root, prefix.Trim('/'));

    public bool IsEnabled => true;
    public string Description => _root;

    private string PathFor(string key)
    {
        var path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(Path.GetFullPath(_root), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Invalid key.", nameof(key));
        return path;
    }

    public Task<IReadOnlyList<StoredObject>> ListAsync(string prefix, CancellationToken ct)
    {
        IReadOnlyList<StoredObject> result = !Directory.Exists(_root) ? [] : Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)
            .Select(f => (File: new FileInfo(f), Key: Path.GetRelativePath(_root, f).Replace('\\', '/')))
            .Where(x => x.Key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(x => new StoredObject(x.Key, x.File.Length, x.File.LastWriteTimeUtc))
            .ToList();
        return Task.FromResult(result);
    }

    public Task UploadAsync(string key, string localPath, string contentType, CancellationToken ct)
    {
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Copy(localPath, path, overwrite: true);
        return Task.CompletedTask;
    }

    public Task<bool> DownloadAsync(string key, string localPath, CancellationToken ct)
    {
        var path = PathFor(key);
        if (!File.Exists(path)) return Task.FromResult(false);
        File.Copy(path, localPath, overwrite: true);
        return Task.FromResult(true);
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        File.Delete(PathFor(key));
        return Task.CompletedTask;
    }
}
