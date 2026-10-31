using Microsoft.EntityFrameworkCore.Diagnostics;
using Portfolio.Api.Services.Backup;

namespace Portfolio.Api.Data;

/// <summary>Schedules a database backup after every SaveChanges that wrote rows.</summary>
public class BackupSaveChangesInterceptor(DatabaseBackupService backups) : SaveChangesInterceptor
{
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (result > 0) backups.MarkDirty();
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (result > 0) backups.MarkDirty();
        return ValueTask.FromResult(result);
    }
}
