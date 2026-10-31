using Acornima;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Portfolio.Api.Data;
using Portfolio.Api.Services;
using Portfolio.Api.Services.Backup;

namespace Portfolio.Api.Tests;

public sealed class BackupTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("portfolio-tests-").FullName;
    private string BackupDir => Path.Combine(_temp, "backups");
    private string SnapshotDir => Path.Combine(BackupDir, "test", "db");

    private WebApplicationFactory<Program> App(string instance, Action<IServiceCollection>? services = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.UseSetting("Database:Path", Path.Combine(_temp, instance, "portfolio.db"));
            b.UseSetting("Storage:RootPath", Path.Combine(_temp, instance, "uploads"));
            b.UseSetting("Backup:Enabled", "true");
            b.UseSetting("Backup:Provider", "FileSystem");
            b.UseSetting("Backup:LocalPath", BackupDir);
            b.UseSetting("Backup:Prefix", "test/");
            b.UseSetting("Backup:DebounceSeconds", "1");
            if (services is not null) b.ConfigureTestServices(services);
        });

    private static async Task<string> SaveProfileNameAsync(WebApplicationFactory<Program> app, string name)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        var profile = await db.Profiles.SingleAsync();
        profile.FullName = name;
        await db.SaveChangesAsync();
        return (await scope.ServiceProvider.GetRequiredService<DatabaseBackupService>().BackupNowAsync(CancellationToken.None)).Key;
    }

    private static async Task<string> ProfileNameAsync(WebApplicationFactory<Program> app)
    {
        using var scope = app.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<PortfolioDbContext>().Profiles.SingleAsync()).FullName;
    }

    [Fact]
    public async Task Fresh_instance_restores_latest_backup_and_does_not_reseed()
    {
        await using (var first = App("a"))
        {
            first.CreateClient();
            await SaveProfileNameAsync(first, "Edited Name");
        }

        await using var second = App("b");   // empty disk, as after a Render restart
        second.CreateClient();
        Assert.Equal("Edited Name", await ProfileNameAsync(second));
        Assert.StartsWith("db/portfolio-", second.Services.GetRequiredService<DatabaseBackupService>().Status.RestoredFrom);
    }

    [Fact]
    public async Task Existing_local_database_is_never_overwritten()
    {
        await using (var first = App("a"))
        {
            first.CreateClient();
            await SaveProfileNameAsync(first, "Local Edit");
        }
        await using (var other = App("b"))
        {
            other.CreateClient();
            await SaveProfileNameAsync(other, "Newer Remote Edit");
        }

        await using var again = App("a");
        again.CreateClient();
        Assert.Equal("Local Edit", await ProfileNameAsync(again));
        Assert.Null(again.Services.GetRequiredService<DatabaseBackupService>().Status.RestoredFrom);
    }

    [Fact]
    public async Task Unreachable_storage_with_missing_database_refuses_to_start()
    {
        await using var app = App("a", s => s.AddSingleton<IObjectStore, FailingStore>());
        var ex = Assert.ThrowsAny<Exception>(() => app.CreateClient());
        Assert.Contains("will not start with an empty database", ex.ToString());
        Assert.False(File.Exists(Path.Combine(_temp, "a", "portfolio.db")));
    }

    [Fact]
    public async Task Corrupt_newest_backup_falls_back_to_previous_valid_one()
    {
        string goodKey;
        await using (var first = App("a"))
        {
            first.CreateClient();
            goodKey = await SaveProfileNameAsync(first, "Good Copy");
        }
        File.WriteAllText(Path.Combine(SnapshotDir, "portfolio-29990101T000000000Z.db"), "not a database");

        await using var second = App("b");
        second.CreateClient();
        Assert.Equal("Good Copy", await ProfileNameAsync(second));
        Assert.Equal(goodKey, second.Services.GetRequiredService<DatabaseBackupService>().Status.RestoredFrom);
    }

    [Fact]
    public async Task Uploads_are_mirrored_and_restored()
    {
        string name;
        await using (var first = App("a"))
        {
            first.CreateClient();
            var storage = first.Services.GetRequiredService<FileStorageService>();
            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6, 7, 8 };
            name = (await storage.SaveAsync(new MemoryStream(png), "photo.png", png.Length, FileCategory.Media)).StoredFileName;
        }

        await using var second = App("b");
        second.CreateClient();
        Assert.NotNull(second.Services.GetRequiredService<FileStorageService>().GetPath(FileCategory.Media, name));
    }

    [Fact]
    public void Remote_keys_cannot_escape_the_upload_folder()
    {
        using var app = App("a");
        app.CreateClient();
        var storage = app.Services.GetRequiredService<FileStorageService>();
        Assert.Null(storage.LocalPathForRemoteKey("uploads/../../portfolio.db"));
        Assert.Null(storage.LocalPathForRemoteKey("uploads/secrets/0123456789abcdef0123456789abcdef.png"));
        Assert.NotNull(storage.LocalPathForRemoteKey("uploads/media/0123456789abcdef0123456789abcdef.png"));
    }

    [Fact]
    public void Retention_keeps_newest_and_one_per_day()
    {
        var now = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
        var keys = Enumerable.Range(0, 100).Select(i => DatabaseBackupService.SnapshotKey(now.AddHours(-i * 6))).ToList();

        var delete = DatabaseBackupService.SelectForDeletion(keys, retainCount: 5, retainDays: 10, now);
        var kept = keys.Except(delete).ToList();

        Assert.Contains(keys[0], kept);
        Assert.True(keys.Take(5).All(kept.Contains));
        Assert.Equal(kept.Count, kept.Distinct().Count());
        Assert.InRange(kept.Count, 11, 16);   // 5 newest + newest of each of the last ~11 days (overlapping)
        Assert.DoesNotContain(keys[^1], kept);
    }

    [Theory]
    [InlineData("https://s3.us-east-005.backblazeb2.com", "auto", "us-east-005")]
    [InlineData("https://abc123.r2.cloudflarestorage.com", "auto", "auto")]
    [InlineData("https://s3.us-east-005.backblazeb2.com", "eu-central-003", "eu-central-003")]
    public void Region_is_taken_from_the_endpoint_when_auto(string url, string region, string expected) =>
        Assert.Equal(expected, S3ObjectStore.RegionFor(url, region));

    [Fact]
    public void Verify_rejects_non_database_files()
    {
        var path = Path.Combine(_temp, "junk.db");
        File.WriteAllText(path, "hello");
        Assert.False(SqliteTools.Verify(path).Ok);
        Assert.False(SqliteTools.Verify(Path.Combine(_temp, "missing.db")).Ok);
    }

    [Fact]
    public void Dashboard_script_parses()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Portfolio.Api", "wwwroot", "js", "admin", "pages", "dashboard.js");
        new Parser().ParseModule(File.ReadAllText(file));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_temp, recursive: true); } catch (IOException) { }
    }

    private sealed class FailingStore : IObjectStore
    {
        public bool IsEnabled => true;
        public string Description => "failing";
        public Task<IReadOnlyList<StoredObject>> ListAsync(string prefix, CancellationToken ct) => throw new HttpRequestException("unreachable");
        public Task UploadAsync(string key, string localPath, string contentType, CancellationToken ct) => throw new HttpRequestException("unreachable");
        public Task<bool> DownloadAsync(string key, string localPath, CancellationToken ct) => throw new HttpRequestException("unreachable");
        public Task DeleteAsync(string key, CancellationToken ct) => throw new HttpRequestException("unreachable");
    }
}
