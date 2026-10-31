using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace Portfolio.Api.Services.Backup;

public record BackupStatus(
    bool Enabled, string Location, bool Ready, bool PendingChanges,
    DateTime? LastBackupUtc, string? LastBackupKey, DateTime? LastErrorUtc, string? LastError, string? RestoredFrom);

public record BackupResult(string Key, long SizeBytes, DateTime CreatedUtc);

/// <summary>
/// Keeps an off-site copy of the SQLite database (and uploads) for hosts with an ephemeral filesystem.
///
/// Startup: if the local database file exists it is used as-is and never overwritten. If it is missing, the newest
/// snapshot that passes integrity checks is restored; if backup storage cannot be read the app refuses to start,
/// so an empty database can never become the newest backup.
///
/// Running: every successful SaveChanges marks the database dirty; after a short quiet period a consistent snapshot
/// is taken with SQLite's online backup API, verified, and uploaded under a new timestamped key (nothing is
/// overwritten). Old snapshots are pruned by count and by day. A final snapshot is attempted on shutdown.
/// </summary>
public partial class DatabaseBackupService(
    IObjectStore store, BackupOptions options, DatabaseLocation database, FileStorageService files,
    ILogger<DatabaseBackupService> logger) : BackgroundService
{
    public const string SnapshotPrefix = "db/";
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ShutdownBudget = TimeSpan.FromSeconds(20);

    private readonly Channel<bool> _signal = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile bool _dirty;
    private volatile bool _ready;
    private DateTime? _lastBackupUtc, _lastErrorUtc;
    private string? _lastBackupKey, _lastError, _restoredFrom;

    public bool Enabled => store.IsEnabled && database.IsFile;

    public BackupStatus Status => new(Enabled, store.Description, _ready, _dirty, _lastBackupUtc, _lastBackupKey, _lastErrorUtc, _lastError, _restoredFrom);

    /// <summary>Called after the database changes. Cheap and safe to call often.</summary>
    public void MarkDirty()
    {
        if (!Enabled) return;
        _dirty = true;
        _signal.Writer.TryWrite(true);
    }

    public static string SnapshotKey(DateTime utc) => $"{SnapshotPrefix}portfolio-{utc:yyyyMMdd'T'HHmmssfff'Z'}.db";

    // ---------------------------------------------------------------- startup restore

    /// <summary>Runs before migrations. Restores the database and missing uploads when the local copy is gone.</summary>
    public async Task RestoreOnStartupAsync(CancellationToken ct)
    {
        if (!Enabled)
        {
            if (Environment.GetEnvironmentVariable("RENDER") is not null)
                logger.LogWarning("Backups are disabled on Render. The database and uploads are lost on every deploy, restart and idle spin-down. Configure Backup__* environment variables.");
            return;
        }

        logger.LogInformation("Backup storage: {Location}", store.Description);
        if (!string.IsNullOrWhiteSpace(options.RestoreObjectKey))
            logger.LogWarning("Backup__RestoreObjectKey is set to {Key}. Remove it once the restore is confirmed, or every fresh start will restore that snapshot again.", options.RestoreObjectKey);

        if (File.Exists(database.FilePath) && new FileInfo(database.FilePath).Length > 0)
        {
            // Never replace an existing database. Refuse to run on a damaged one rather than back it up.
            var local = SqliteTools.Verify(database.FilePath, requireAppSchema: false);
            if (!local.Ok)
                throw new InvalidOperationException($"The local database {database.FilePath} failed its integrity check ({local.Message}). It was not modified. Move it aside to restore from backup, or repair it manually.");
            logger.LogInformation("Local database found; startup restore skipped (an existing database is never overwritten).");
        }
        else
        {
            await RestoreDatabaseAsync(ct);
        }

        await RestoreMissingUploadsAsync(ct);
        _ready = true;
        if (_dirty) _signal.Writer.TryWrite(true);
    }

    private async Task RestoreDatabaseAsync(CancellationToken ct)
    {
        List<StoredObject> snapshots;
        try
        {
            snapshots = (await store.ListAsync(SnapshotPrefix, ct)).Where(o => SnapshotName().IsMatch(o.Key)).OrderByDescending(o => o.Key, StringComparer.Ordinal).ToList();
        }
        catch (Exception ex) when (options.RequireRestoreOnStartup && ex is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                "The local database is missing and backup storage could not be read, so the app will not start with an empty database. " +
                "Check the Backup__* settings and the storage service, then restart.", ex);
        }

        var pinned = options.RestoreObjectKey?.Trim();
        var candidates = string.IsNullOrEmpty(pinned) ? snapshots.Select(s => s.Key).ToList() : [pinned];
        if (candidates.Count == 0)
        {
            logger.LogWarning("No database backup found at {Location}. Starting with a new database; it will be backed up after the first change.", store.Description);
            return;
        }

        MoveAside(database.FilePath, "empty");   // a 0-byte file
        MoveAside(database.FilePath + "-wal", "stale");
        MoveAside(database.FilePath + "-shm", "stale");
        Directory.CreateDirectory(Path.GetDirectoryName(database.FilePath)!);

        foreach (var key in candidates.Take(10))
        {
            var temp = database.FilePath + ".restore-" + Guid.NewGuid().ToString("N");
            try
            {
                if (!await store.DownloadAsync(key, temp, ct))
                {
                    if (!string.IsNullOrEmpty(pinned)) throw new InvalidOperationException($"Backup__RestoreObjectKey {key} was not found in {store.Description}.");
                    continue;
                }
                var report = SqliteTools.Verify(temp);
                if (!report.Ok)
                {
                    logger.LogError("Backup {Key} failed verification ({Reason}); trying an older one.", key, report.Message);
                    continue;
                }
                File.Move(temp, database.FilePath);  // throws rather than overwrite if a file appeared meanwhile
                _restoredFrom = key;
                logger.LogInformation("Restored database from {Key} ({Size} bytes, {Migrations} migrations, latest {Latest}).", key, report.SizeBytes, report.Migrations, report.LatestMigration);

                // A pinned or fallback restore must become the newest snapshot, or the next start would pick a different one.
                if (key != snapshots.FirstOrDefault()?.Key) _dirty = true;
                return;
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        throw new InvalidOperationException($"No valid database backup could be restored from {store.Description}. Nothing was changed. See docs/BACKUP_AND_RECOVERY.md for manual recovery.");
    }

    private async Task RestoreMissingUploadsAsync(CancellationToken ct)
    {
        try
        {
            var restored = 0;
            foreach (var item in await store.ListAsync(FileStorageService.RemotePrefix, ct))
            {
                var path = files.LocalPathForRemoteKey(item.Key);
                if (path is null || File.Exists(path)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var temp = path + ".partial";
                if (await store.DownloadAsync(item.Key, temp, ct)) { File.Move(temp, path, overwrite: false); restored++; }
            }
            if (restored > 0) logger.LogInformation("Restored {Count} uploaded file(s) from backup storage.", restored);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The remote copies are untouched; the next start retries. Missing files return 404 until then.
            logger.LogError(ex, "Could not restore uploaded files from backup storage.");
        }
    }

    private void MoveAside(string path, string reason)
    {
        if (!File.Exists(path)) return;
        var target = $"{path}.{reason}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        File.Move(path, target);
        logger.LogWarning("Moved {Path} aside to {Target}.", path, target);
    }

    // ---------------------------------------------------------------- snapshots

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Enabled) return;
        try
        {
            while (true)
            {
                await _signal.Reader.ReadAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(options.DebounceSeconds), stoppingToken);
                if (!_dirty || !_ready) continue;
                if (!await TryBackupAsync(stoppingToken))
                {
                    await Task.Delay(RetryDelay, stoppingToken);
                    _signal.Writer.TryWrite(true);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }

        if (_dirty && _ready)
        {
            using var cts = new CancellationTokenSource(ShutdownBudget);
            logger.LogInformation("Taking a final backup before shutdown.");
            await TryBackupAsync(cts.Token);
        }
    }

    private async Task<bool> TryBackupAsync(CancellationToken ct)
    {
        try { await BackupNowAsync(ct); return true; }
        catch (Exception ex) { logger.LogError(ex, "Database backup failed; it will be retried."); return false; }
    }

    /// <summary>Takes, verifies and uploads a snapshot now, then prunes old ones.</summary>
    public async Task<BackupResult> BackupNowAsync(CancellationToken ct)
    {
        if (!Enabled) throw new InvalidOperationException("Backups are not configured.");
        if (!_ready) throw new InvalidOperationException("Backups start after the startup restore has completed.");

        await _gate.WaitAsync(ct);
        var temp = Path.Combine(Path.GetTempPath(), $"portfolio-backup-{Guid.NewGuid():N}.db");
        try
        {
            _dirty = false; // cleared first: changes made during the snapshot mark it dirty again
            SqliteTools.Snapshot(database.ConnectionString, temp);
            var report = SqliteTools.Verify(temp);
            if (!report.Ok) throw new InvalidOperationException("The snapshot failed verification: " + report.Message);

            var now = DateTime.UtcNow;
            var key = SnapshotKey(now);
            await store.UploadAsync(key, temp, "application/vnd.sqlite3", ct);
            (_lastBackupUtc, _lastBackupKey, _lastError, _lastErrorUtc) = (now, key, null, null);
            logger.LogInformation("Database backed up to {Key} ({Size} bytes).", key, report.SizeBytes);

            try { await PruneAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning(ex, "Could not prune old backups."); }
            return new BackupResult(key, report.SizeBytes, now);
        }
        catch (Exception ex)
        {
            _dirty = true;
            (_lastError, _lastErrorUtc) = (ex.Message, DateTime.UtcNow);
            throw;
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<StoredObject>> ListSnapshotsAsync(CancellationToken ct) =>
        (await store.ListAsync(SnapshotPrefix, ct)).Where(o => SnapshotName().IsMatch(o.Key)).OrderByDescending(o => o.Key, StringComparer.Ordinal).ToList();

    /// <summary>Downloads a snapshot to a temporary file and runs the integrity checks on it. Changes nothing.</summary>
    public async Task<IntegrityReport> VerifySnapshotAsync(string key, CancellationToken ct)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"portfolio-verify-{Guid.NewGuid():N}.db");
        try
        {
            return await store.DownloadAsync(key, temp, ct) ? SqliteTools.Verify(temp) : new IntegrityReport(false, "Backup not found.");
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    /// <summary>Keeps the newest RetainCount snapshots plus the newest one of each of the last RetainDays days.</summary>
    public static IReadOnlyList<string> SelectForDeletion(IEnumerable<string> keys, int retainCount, int retainDays, DateTime nowUtc)
    {
        var dated = keys.Select(k => (Key: k, Match: SnapshotName().Match(k))).Where(x => x.Match.Success)
            .Select(x => (x.Key, Time: DateTime.ParseExact(x.Match.Groups[1].Value, "yyyyMMdd'T'HHmmssfff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)))
            .OrderByDescending(x => x.Key, StringComparer.Ordinal).ToList();

        var keep = dated.Take(retainCount).Select(x => x.Key).ToHashSet();
        foreach (var day in dated.Where(x => x.Time >= nowUtc.Date.AddDays(-retainDays)).GroupBy(x => x.Time.Date))
            keep.Add(day.First().Key);
        return dated.Where(x => !keep.Contains(x.Key)).Select(x => x.Key).ToList();
    }

    private async Task PruneAsync(CancellationToken ct)
    {
        var all = await store.ListAsync(SnapshotPrefix, ct);
        foreach (var key in SelectForDeletion(all.Select(o => o.Key), options.RetainCount, options.RetainDays, DateTime.UtcNow))
        {
            await store.DeleteAsync(key, ct);
            logger.LogInformation("Pruned old backup {Key}.", key);
        }
    }

    [GeneratedRegex(@"^db/portfolio-(\d{8}T\d{9})Z\.db$")]
    private static partial Regex SnapshotName();
}
