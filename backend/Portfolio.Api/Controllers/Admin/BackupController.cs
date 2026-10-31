using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Api.Services.Backup;

namespace Portfolio.Api.Controllers.Admin;

/// <summary>
/// Backup status, on-demand backups and integrity checks. Restoring is deliberately not available here: it replaces
/// the live database, so it is done on startup (see docs/BACKUP_AND_RECOVERY.md).
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin/backup")]
public class BackupController(DatabaseBackupService backups, DatabaseLocation database) : ControllerBase
{
    [HttpGet]
    public async Task<object> Get(CancellationToken ct)
    {
        IReadOnlyList<StoredObject> snapshots = [];
        string? listError = null;
        if (backups.Enabled)
        {
            try { snapshots = await backups.ListSnapshotsAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { listError = ex.Message; }
        }
        return new { status = backups.Status, listError, snapshots = snapshots.Take(50) };
    }

    [HttpPost]
    public async Task<ActionResult<BackupResult>> BackupNow(CancellationToken ct)
    {
        try { return await backups.BackupNowAsync(ct); }
        catch (InvalidOperationException ex) { return Problem(title: ex.Message, statusCode: StatusCodes.Status409Conflict); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Problem(title: "Backup failed: " + ex.Message, statusCode: StatusCodes.Status502BadGateway); }
    }

    /// <summary>Checks the live database and the newest (or a given) snapshot. Read-only.</summary>
    [HttpPost("verify")]
    public async Task<object> Verify([FromQuery] string? key, CancellationToken ct)
    {
        var live = database.IsFile ? SqliteTools.Verify(database.FilePath) : new IntegrityReport(false, "Not a file database.");
        IntegrityReport? snapshot = null;
        string? snapshotKey = key;
        if (backups.Enabled)
        {
            try
            {
                snapshotKey ??= (await backups.ListSnapshotsAsync(ct)).FirstOrDefault()?.Key;
                snapshot = snapshotKey is null ? new IntegrityReport(false, "No backups yet.") : await backups.VerifySnapshotAsync(snapshotKey, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { snapshot = new IntegrityReport(false, ex.Message); }
        }
        return new { live, snapshotKey, snapshot };
    }
}
