using Microsoft.Data.Sqlite;
using Partcounter.Models;

namespace Partcounter.Services;

public sealed class ActiveOrderRecoveryService
{
    private readonly string _databasePath;

    public ActiveOrderRecoveryService(string databasePath)
    {
        _databasePath = Path.GetFullPath(databasePath);
    }

    private string ConnectionString => SqliteWriteCoordinator.BuildConnectionString(_databasePath);

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        SqliteWriteCoordinator.ExecuteAsync(_databasePath, async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS ActiveOrderRecovery (
                    MachineNumber INTEGER PRIMARY KEY,
                    OrderNumber TEXT NOT NULL,
                    JobId INTEGER NOT NULL,
                    ArticleNumber TEXT NOT NULL,
                    ArticleDescription TEXT NOT NULL,
                    ToolNumber TEXT NOT NULL,
                    ActiveCavities INTEGER NOT NULL,
                    StandardVeTarget INTEGER NOT NULL,
                    OrderTargetQuantity INTEGER NOT NULL,
                    OrderState INTEGER NOT NULL,
                    ScheduledHoldAfterVeNumber INTEGER NOT NULL,
                    ManualVeReconfigurationPending INTEGER NOT NULL,
                    IsTemporarilyDisabled INTEGER NOT NULL,
                    LastKnownOrderProducedQuantity INTEGER NOT NULL,
                    LastKnownCurrentParts INTEGER NOT NULL,
                    LastKnownTotalCycles INTEGER NOT NULL,
                    LastKnownCurrentVeNumber INTEGER NOT NULL,
                    LastKnownCompletedVes INTEGER NOT NULL,
                    LastKnownLastCompletedVeQuantity INTEGER NOT NULL,
                    Phase INTEGER NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);

    public async Task<IReadOnlyList<ActiveOrderCheckpoint>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<ActiveOrderCheckpoint>();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT MachineNumber, OrderNumber, JobId, ArticleNumber, ArticleDescription, ToolNumber,
                   ActiveCavities, StandardVeTarget, OrderTargetQuantity, OrderState, ScheduledHoldAfterVeNumber,
                   ManualVeReconfigurationPending, IsTemporarilyDisabled, LastKnownOrderProducedQuantity,
                   LastKnownCurrentParts, LastKnownTotalCycles, LastKnownCurrentVeNumber, LastKnownCompletedVes,
                   LastKnownLastCompletedVeQuantity, Phase, UpdatedAtUtc
            FROM ActiveOrderRecovery
            ORDER BY MachineNumber;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ActiveOrderCheckpoint(
                reader.GetInt32(0),
                reader.GetString(1),
                checked((uint)reader.GetInt64(2)),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                checked((ushort)reader.GetInt32(6)),
                checked((uint)reader.GetInt64(7)),
                checked((uint)reader.GetInt64(8)),
                (ProductionOrderState)reader.GetInt32(9),
                checked((ushort)reader.GetInt32(10)),
                reader.GetInt32(11) != 0,
                reader.GetInt32(12) != 0,
                checked((uint)reader.GetInt64(13)),
                checked((uint)reader.GetInt64(14)),
                checked((uint)reader.GetInt64(15)),
                checked((ushort)reader.GetInt32(16)),
                checked((ushort)reader.GetInt32(17)),
                checked((uint)reader.GetInt64(18)),
                (ActiveOrderCheckpointPhase)reader.GetInt32(19),
                DateTime.Parse(reader.GetString(20), null, System.Globalization.DateTimeStyles.RoundtripKind)));
        }

        return result;
    }

    public Task UpsertAsync(ActiveOrderCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        if (checkpoint.MachineNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(checkpoint.MachineNumber));
        if (string.IsNullOrWhiteSpace(checkpoint.OrderNumber))
            throw new ArgumentException("Recovery-Auftragsnummer darf nicht leer sein.", nameof(checkpoint));
        if (checkpoint.ActiveCavities is < 1 or > 64 || checkpoint.StandardVeTarget == 0 || checkpoint.OrderTargetQuantity == 0)
            throw new ArgumentException("Recovery-Auftragsparameter sind ungültig.", nameof(checkpoint));

        return SqliteWriteCoordinator.ExecuteAsync(_databasePath, async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO ActiveOrderRecovery(
                    MachineNumber, OrderNumber, JobId, ArticleNumber, ArticleDescription, ToolNumber, ActiveCavities,
                    StandardVeTarget, OrderTargetQuantity, OrderState, ScheduledHoldAfterVeNumber,
                    ManualVeReconfigurationPending, IsTemporarilyDisabled, LastKnownOrderProducedQuantity,
                    LastKnownCurrentParts, LastKnownTotalCycles, LastKnownCurrentVeNumber, LastKnownCompletedVes,
                    LastKnownLastCompletedVeQuantity, Phase, UpdatedAtUtc)
                VALUES(
                    $machine, $order, $jobId, $article, $description, $tool, $cavities, $veTarget, $orderTarget,
                    $state, $hold, $manualPending, $disabled, $produced, $currentParts, $totalCycles, $currentVe,
                    $completedVes, $lastCompletedQuantity, $phase, $updated)
                ON CONFLICT(MachineNumber) DO UPDATE SET
                    OrderNumber=excluded.OrderNumber, JobId=excluded.JobId, ArticleNumber=excluded.ArticleNumber,
                    ArticleDescription=excluded.ArticleDescription, ToolNumber=excluded.ToolNumber,
                    ActiveCavities=excluded.ActiveCavities, StandardVeTarget=excluded.StandardVeTarget,
                    OrderTargetQuantity=excluded.OrderTargetQuantity, OrderState=excluded.OrderState,
                    ScheduledHoldAfterVeNumber=excluded.ScheduledHoldAfterVeNumber,
                    ManualVeReconfigurationPending=excluded.ManualVeReconfigurationPending,
                    IsTemporarilyDisabled=excluded.IsTemporarilyDisabled,
                    LastKnownOrderProducedQuantity=excluded.LastKnownOrderProducedQuantity,
                    LastKnownCurrentParts=excluded.LastKnownCurrentParts, LastKnownTotalCycles=excluded.LastKnownTotalCycles,
                    LastKnownCurrentVeNumber=excluded.LastKnownCurrentVeNumber, LastKnownCompletedVes=excluded.LastKnownCompletedVes,
                    LastKnownLastCompletedVeQuantity=excluded.LastKnownLastCompletedVeQuantity,
                    Phase=excluded.Phase, UpdatedAtUtc=excluded.UpdatedAtUtc;
                """;
            command.Parameters.AddWithValue("$machine", checkpoint.MachineNumber);
            command.Parameters.AddWithValue("$order", checkpoint.OrderNumber);
            command.Parameters.AddWithValue("$jobId", (long)checkpoint.JobId);
            command.Parameters.AddWithValue("$article", checkpoint.ArticleNumber);
            command.Parameters.AddWithValue("$description", checkpoint.ArticleDescription);
            command.Parameters.AddWithValue("$tool", checkpoint.ToolNumber);
            command.Parameters.AddWithValue("$cavities", (int)checkpoint.ActiveCavities);
            command.Parameters.AddWithValue("$veTarget", (long)checkpoint.StandardVeTarget);
            command.Parameters.AddWithValue("$orderTarget", (long)checkpoint.OrderTargetQuantity);
            command.Parameters.AddWithValue("$state", (int)checkpoint.OrderState);
            command.Parameters.AddWithValue("$hold", (int)checkpoint.ScheduledHoldAfterVeNumber);
            command.Parameters.AddWithValue("$manualPending", checkpoint.ManualVeReconfigurationPending ? 1 : 0);
            command.Parameters.AddWithValue("$disabled", checkpoint.IsTemporarilyDisabled ? 1 : 0);
            command.Parameters.AddWithValue("$produced", (long)checkpoint.LastKnownOrderProducedQuantity);
            command.Parameters.AddWithValue("$currentParts", (long)checkpoint.LastKnownCurrentParts);
            command.Parameters.AddWithValue("$totalCycles", (long)checkpoint.LastKnownTotalCycles);
            command.Parameters.AddWithValue("$currentVe", (int)checkpoint.LastKnownCurrentVeNumber);
            command.Parameters.AddWithValue("$completedVes", (int)checkpoint.LastKnownCompletedVes);
            command.Parameters.AddWithValue("$lastCompletedQuantity", (long)checkpoint.LastKnownLastCompletedVeQuantity);
            command.Parameters.AddWithValue("$phase", (int)checkpoint.Phase);
            command.Parameters.AddWithValue("$updated", checkpoint.UpdatedAtUtc.ToUniversalTime().ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public Task DeleteAsync(int machineNumber, CancellationToken cancellationToken = default) =>
        SqliteWriteCoordinator.ExecuteAsync(_databasePath, async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM ActiveOrderRecovery WHERE MachineNumber=$machine;";
            command.Parameters.AddWithValue("$machine", machineNumber);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
}
