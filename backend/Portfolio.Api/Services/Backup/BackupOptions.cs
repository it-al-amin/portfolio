namespace Portfolio.Api.Services.Backup;

/// <summary>
/// Off-site copies of the SQLite database and uploads ("Backup" section, e.g. Backup__Enabled=true).
/// Needed on hosts whose filesystem is wiped on restart/deploy, such as Render's Free plan.
/// </summary>
public class BackupOptions
{
    public bool Enabled { get; set; }

    /// <summary>"S3" (any S3-compatible service: Cloudflare R2, Backblaze B2, AWS S3, MinIO) or "FileSystem" (a local folder, for development and tests).</summary>
    public string Provider { get; set; } = "S3";

    /// <summary>S3 endpoint, e.g. https://&lt;account-id&gt;.r2.cloudflarestorage.com. Leave empty for AWS S3.</summary>
    public string? ServiceUrl { get; set; }
    public string Region { get; set; } = "auto";
    public string? Bucket { get; set; }
    public string? AccessKeyId { get; set; }
    public string? SecretAccessKey { get; set; }
    public bool ForcePathStyle { get; set; } = true;

    /// <summary>Folder for the FileSystem provider. Relative paths resolve against the content root.</summary>
    public string? LocalPath { get; set; }

    /// <summary>Key prefix inside the bucket, so one bucket can hold several environments.</summary>
    public string Prefix { get; set; } = "portfolio/";

    /// <summary>Wait this long after the last database change before taking a snapshot, so a burst of edits makes one backup.</summary>
    public int DebounceSeconds { get; set; } = 10;

    /// <summary>Always keep this many of the newest snapshots.</summary>
    public int RetainCount { get; set; } = 30;

    /// <summary>Additionally keep the newest snapshot of each of the last N days.</summary>
    public int RetainDays { get; set; } = 30;

    /// <summary>
    /// When the local database is missing and backup storage cannot be read, refuse to start instead of creating an
    /// empty database (which would then be backed up as the newest snapshot). Only disable this deliberately.
    /// </summary>
    public bool RequireRestoreOnStartup { get; set; } = true;

    /// <summary>Restore this snapshot key (e.g. db/portfolio-20261031T101500000Z.db) instead of the newest one. Remove it after the restore.</summary>
    public string? RestoreObjectKey { get; set; }

    public void Validate()
    {
        if (!Enabled) return;
        if (Provider.Equals("S3", StringComparison.OrdinalIgnoreCase))
        {
            var missing = new[] { (nameof(Bucket), Bucket), (nameof(AccessKeyId), AccessKeyId), (nameof(SecretAccessKey), SecretAccessKey) }
                .Where(x => string.IsNullOrWhiteSpace(x.Item2)).Select(x => "Backup__" + x.Item1).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException($"Backups are enabled but {string.Join(", ", missing)} is not set.");
        }
        else if (Provider.Equals("FileSystem", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(LocalPath)) throw new InvalidOperationException("Backup__LocalPath is required for the FileSystem backup provider.");
        }
        else throw new InvalidOperationException($"Unknown Backup__Provider '{Provider}'. Use S3 or FileSystem.");

        if (RetainCount < 3) throw new InvalidOperationException("Backup__RetainCount must be at least 3.");
    }
}

/// <summary>The resolved SQLite file and connection string the app uses.</summary>
public record DatabaseLocation(string FilePath, string ConnectionString)
{
    public bool IsFile => !FilePath.StartsWith(':') && !FilePath.StartsWith("file:");
}
