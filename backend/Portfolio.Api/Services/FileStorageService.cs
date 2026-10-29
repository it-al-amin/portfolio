using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Portfolio.Api.Services;

public enum FileCategory { Documents, Media }

public record StoredFile(string StoredFileName, string OriginalFileName, string ContentType, long SizeBytes);

public class FileValidationException(string message) : Exception(message);

/// <summary>
/// Stores uploads outside wwwroot under random names. File types are decided by extension allow-list
/// plus a magic-byte check; the client-supplied content type and file name are never trusted.
/// </summary>
public partial class FileStorageService
{
    private record FileType(string ContentType, Func<byte[], bool> Matches);

    private static bool StartsWith(byte[] data, params byte[] sig) => data.Length >= sig.Length && data.AsSpan(0, sig.Length).SequenceEqual(sig);
    private static readonly Func<byte[], bool> IsPdf = d => StartsWith(d, "%PDF-"u8.ToArray());
    private static readonly Func<byte[], bool> IsZip = d => StartsWith(d, 0x50, 0x4B, 0x03, 0x04);
    private static readonly Func<byte[], bool> IsPng = d => StartsWith(d, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
    private static readonly Func<byte[], bool> IsJpeg = d => StartsWith(d, 0xFF, 0xD8, 0xFF);
    private static readonly Func<byte[], bool> IsWebp = d => StartsWith(d, "RIFF"u8.ToArray()) && d.Length >= 12 && d.AsSpan(8, 4).SequenceEqual("WEBP"u8);

    private static readonly Dictionary<string, FileType> ImageTypes = new()
    {
        [".png"] = new("image/png", IsPng),
        [".jpg"] = new("image/jpeg", IsJpeg),
        [".jpeg"] = new("image/jpeg", IsJpeg),
        [".webp"] = new("image/webp", IsWebp),
    };

    private static readonly Dictionary<string, FileType> DocumentTypes = new(ImageTypes)
    {
        [".pdf"] = new("application/pdf", IsPdf),
        [".docx"] = new("application/vnd.openxmlformats-officedocument.wordprocessingml.document", IsZip),
    };

    private readonly StorageOptions _options;
    private readonly string _root;

    public FileStorageService(IOptions<StorageOptions> options, IWebHostEnvironment env)
    {
        _options = options.Value;
        _root = Path.GetFullPath(Path.IsPathRooted(_options.RootPath)
            ? _options.RootPath
            : Path.Combine(env.ContentRootPath, _options.RootPath));
    }

    public static IReadOnlyCollection<string> AllowedDocumentExtensions => DocumentTypes.Keys;
    public static IReadOnlyCollection<string> AllowedImageExtensions => ImageTypes.Keys;

    public async Task<StoredFile> SaveAsync(IFormFile file, FileCategory category, CancellationToken ct = default)
    {
        await using var stream = file.OpenReadStream();
        return await SaveAsync(stream, file.FileName, file.Length, category, ct);
    }

    public async Task<StoredFile> SaveAsync(Stream content, string originalFileName, long length, FileCategory category, CancellationToken ct = default)
    {
        var types = category == FileCategory.Media ? ImageTypes : DocumentTypes;
        var max = category == FileCategory.Media ? _options.MaxImageBytes : _options.MaxDocumentBytes;

        if (length <= 0) throw new FileValidationException("The file is empty.");
        if (length > max) throw new FileValidationException($"The file exceeds the {max / (1024 * 1024)} MB limit.");

        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (!types.TryGetValue(ext, out var type))
            throw new FileValidationException($"Unsupported file type. Allowed: {string.Join(", ", types.Keys)}.");

        var header = new byte[16];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        if (!type.Matches(header[..read]))
            throw new FileValidationException("The file content does not match its extension.");

        var storedName = $"{Guid.NewGuid():N}{ext}";
        var dir = Path.Combine(_root, category.ToString().ToLowerInvariant());
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, storedName);

        await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
        {
            await output.WriteAsync(header.AsMemory(0, read), ct);
            await content.CopyToAsync(output, ct);
        }

        if (new FileInfo(path).Length > max)
        {
            File.Delete(path);
            throw new FileValidationException($"The file exceeds the {max / (1024 * 1024)} MB limit.");
        }

        return new StoredFile(storedName, SafeDisplayName(originalFileName, ext), type.ContentType, new FileInfo(path).Length);
    }

    /// <summary>Resolves a stored file to a physical path, or null if the name is not one we generated.</summary>
    public string? GetPath(FileCategory category, string storedFileName)
    {
        if (!StoredNamePattern().IsMatch(storedFileName)) return null;
        var path = Path.Combine(_root, category.ToString().ToLowerInvariant(), storedFileName);
        return File.Exists(path) ? path : null;
    }

    public void Delete(FileCategory category, string storedFileName)
    {
        var path = GetPath(category, storedFileName);
        if (path is not null) File.Delete(path);
    }

    /// <summary>Strips path segments and unusual characters from a user-supplied file name for display/download.</summary>
    public static string SafeDisplayName(string fileName, string ext)
    {
        var name = Path.GetFileNameWithoutExtension(Path.GetFileName(fileName.Replace('\\', '/')));
        name = UnsafeChars().Replace(name, "_").Trim('_', '.', ' ');
        if (name.Length > 80) name = name[..80];
        return (string.IsNullOrEmpty(name) ? "file" : name) + ext;
    }

    [GeneratedRegex("^[a-f0-9]{32}\\.[a-z0-9]{2,5}$")]
    private static partial Regex StoredNamePattern();

    [GeneratedRegex("[^A-Za-z0-9 ._()-]+")]
    private static partial Regex UnsafeChars();
}
