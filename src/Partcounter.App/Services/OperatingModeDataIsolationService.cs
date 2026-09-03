using Microsoft.Data.Sqlite;

namespace Partcounter.Services;

/// <summary>
/// Defense-in-depth for HF5 data separation.
/// New PackagingUnits rows are classified by the runtime operating mode through a
/// SQLite trigger. Legacy rows from pre-HF5 builds remain unclassified and are never
/// silently promoted to production history.
/// </summary>
public sealed class OperatingModeDataIsolationService
{
    public const string SimulationMode = "Simulation";
    public const string ProductionMode = "Production";

    private readonly DatabaseService _database = new();

    private string ConnectionString =>
        SqliteWriteCoordinator.BuildConnectionString(_database.DatabasePath);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys=ON;
            PRAGMA busy_timeout=15000;

            CREATE TABLE IF NOT EXISTS Hf5RuntimeModeState (
                Id INTEGER PRIMARY KEY CHECK(Id = 1),
                Mode TEXT NOT NULL,
                Revision TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS PackagingUnitModes (
                PackagingUnitId TEXT PRIMARY KEY,
                Mode TEXT NOT NULL,
                Revision TEXT NOT NULL,
                ClassifiedAtUtc TEXT NOT NULL,
                FOREIGN KEY(PackagingUnitId) REFERENCES PackagingUnits(Id) ON DELETE CASCADE
            );

            DROP TRIGGER IF EXISTS TRG_HF5_PackagingUnitMode;
            CREATE TRIGGER TRG_HF5_PackagingUnitMode
            AFTER INSERT ON PackagingUnits
            BEGIN
                INSERT OR REPLACE INTO PackagingUnitModes
                    (PackagingUnitId, Mode, Revision, ClassifiedAtUtc)
                SELECT
                    NEW.Id,
                    Mode,
                    Revision,
                    strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
                FROM Hf5RuntimeModeState
                WHERE Id = 1;
            END;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await SetModeAsync(SimulationMode, cancellationToken);
    }

    public async Task SetModeAsync(string mode, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(mode, SimulationMode, StringComparison.Ordinal) &&
            !string.Equals(mode, ProductionMode, StringComparison.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(mode));

        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Hf5RuntimeModeState (Id, Mode, Revision, UpdatedAtUtc)
            VALUES (1, $mode, $revision, $updated)
            ON CONFLICT(Id) DO UPDATE SET
                Mode = excluded.Mode,
                Revision = excluded.Revision,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;
        command.Parameters.AddWithValue("$mode", mode);
        command.Parameters.AddWithValue("$revision", AppVersionInfo.RevisionLabel);
        command.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<HashSet<string>> LoadProductionPackagingUnitIdsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT PackagingUnitId
            FROM PackagingUnitModes
            WHERE Mode = $mode;
            """;
        command.Parameters.AddWithValue("$mode", ProductionMode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(reader.GetString(0));
        return result;
    }

    public async Task<(long Production, long Simulation, long LegacyUnknown)> GetHistoryClassificationCountsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                SUM(CASE WHEN pum.Mode = 'Production' THEN 1 ELSE 0 END),
                SUM(CASE WHEN pum.Mode = 'Simulation' THEN 1 ELSE 0 END),
                SUM(CASE WHEN pum.PackagingUnitId IS NULL THEN 1 ELSE 0 END)
            FROM PackagingUnits pu
            LEFT JOIN PackagingUnitModes pum ON pum.PackagingUnitId = pu.Id;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return (0, 0, 0);

        static long ReadLong(SqliteDataReader reader, int ordinal) =>
            reader.IsDBNull(ordinal) ? 0 : reader.GetInt64(ordinal);

        return (ReadLong(reader, 0), ReadLong(reader, 1), ReadLong(reader, 2));
    }
}
