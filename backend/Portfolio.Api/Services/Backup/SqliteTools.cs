using Microsoft.Data.Sqlite;

namespace Portfolio.Api.Services.Backup;

public record IntegrityReport(bool Ok, string Message, int Migrations = 0, string? LatestMigration = null, long SizeBytes = 0);

public static class SqliteTools
{
    private static string ConnectionFor(string path) =>
        new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString();

    /// <summary>
    /// Checks that a file is an intact SQLite database created by this app: PRAGMA integrity_check, foreign keys,
    /// and an EF Core migration history. Never creates or modifies the file's data.
    /// </summary>
    public static IntegrityReport Verify(string path, bool requireAppSchema = true)
    {
        if (!File.Exists(path)) return new(false, "File not found.");
        var size = new FileInfo(path).Length;
        if (size == 0) return new(false, "File is empty.");
        try
        {
            using var connection = new SqliteConnection(ConnectionFor(path));
            connection.Open();

            var integrity = Rows(connection, "PRAGMA integrity_check;");
            if (integrity is not ["ok"]) return new(false, "integrity_check failed: " + string.Join("; ", integrity.Take(5)), SizeBytes: size);

            var fk = Rows(connection, "PRAGMA foreign_key_check;");
            if (fk.Count > 0) return new(false, $"foreign_key_check found {fk.Count} violation(s).", SizeBytes: size);

            var hasHistory = Rows(connection, "SELECT name FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory';").Count > 0;
            if (!hasHistory && requireAppSchema)
                return new(false, "Not a portfolio database: no __EFMigrationsHistory table.", SizeBytes: size);

            var migrations = !hasHistory ? [] : Rows(connection, "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;");
            return new(true, "ok", migrations.Count, migrations.LastOrDefault(), size);
        }
        catch (SqliteException ex)
        {
            return new(false, ex.Message, SizeBytes: size);
        }
    }

    /// <summary>
    /// Writes a consistent copy of the live database with SQLite's online backup API (safe while the app is writing),
    /// as a single self-contained file (rollback journal, not WAL).
    /// </summary>
    public static void Snapshot(string sourceConnectionString, string destinationPath)
    {
        using var source = new SqliteConnection(sourceConnectionString);
        using var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destinationPath, Pooling = false }.ToString());
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
        Rows(destination, "PRAGMA journal_mode = DELETE;");
    }

    private static List<string> Rows(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read()) rows.Add(reader.FieldCount == 1 ? Convert.ToString(reader.GetValue(0))! : string.Join(", ", Enumerable.Range(0, reader.FieldCount).Select(reader.GetValue)));
        return rows;
    }
}
