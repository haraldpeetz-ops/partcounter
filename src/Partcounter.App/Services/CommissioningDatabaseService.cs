using Microsoft.Data.Sqlite;
using Partcounter.Models;

namespace Partcounter.Services;

public sealed class CommissioningDatabaseService
{
    private readonly string _connectionString;

    public CommissioningDatabaseService(string databasePath)
    {
        _connectionString = $"Data Source={databasePath};Cache=Shared";
    }

    public async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS CommissioningProfiles (
                MachineNumber INTEGER PRIMARY KEY,
                LogoOrderNumber TEXT NOT NULL,
                LogoType TEXT NOT NULL,
                SupplyVoltage TEXT NOT NULL,
                CycleInput TEXT NOT NULL,
                CycleSignal TEXT NOT NULL,
                ValveOutput TEXT NOT NULL,
                ValveVoltage TEXT NOT NULL,
                UseInterfaceRelay INTEGER NOT NULL,
                EndPositionMonitoring INTEGER NOT NULL,
                EndPositionInput TEXT NOT NULL,
                DefaultValvePulseMs INTEGER NOT NULL,
                ReleaseState INTEGER NOT NULL,
                Notes TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS CommissioningChecks (
                MachineNumber INTEGER NOT NULL,
                CheckCode TEXT NOT NULL,
                Result INTEGER NOT NULL,
                Note TEXT NOT NULL,
                CheckedAtUtc TEXT NULL,
                PRIMARY KEY (MachineNumber, CheckCode)
            );

            CREATE INDEX IF NOT EXISTS IX_CommissioningProfiles_ReleaseState
                ON CommissioningProfiles(ReleaseState);
            """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<CommissioningProfile?> LoadProfileAsync(int machineNumber)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT MachineNumber, LogoOrderNumber, LogoType, SupplyVoltage, CycleInput, CycleSignal,
                   ValveOutput, ValveVoltage, UseInterfaceRelay, EndPositionMonitoring, EndPositionInput,
                   DefaultValvePulseMs, ReleaseState, Notes, UpdatedAtUtc
            FROM CommissioningProfiles
            WHERE MachineNumber=$machine;
            """;
        command.Parameters.AddWithValue("$machine", machineNumber);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new CommissioningProfile(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetInt32(8) != 0,
            reader.GetInt32(9) != 0,
            reader.GetString(10),
            checked((ushort)reader.GetInt32(11)),
            (CommissioningReleaseState)reader.GetInt32(12),
            reader.GetString(13),
            DateTime.Parse(reader.GetString(14), null, System.Globalization.DateTimeStyles.RoundtripKind));
    }

    public async Task UpsertProfileAsync(CommissioningProfile profile)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO CommissioningProfiles
                (MachineNumber, LogoOrderNumber, LogoType, SupplyVoltage, CycleInput, CycleSignal,
                 ValveOutput, ValveVoltage, UseInterfaceRelay, EndPositionMonitoring, EndPositionInput,
                 DefaultValvePulseMs, ReleaseState, Notes, UpdatedAtUtc)
            VALUES
                ($machine, $orderNumber, $logoType, $supply, $cycleInput, $cycleSignal,
                 $valveOutput, $valveVoltage, $relay, $endMonitoring, $endInput,
                 $pulse, $release, $notes, $updated)
            ON CONFLICT(MachineNumber) DO UPDATE SET
                LogoOrderNumber=excluded.LogoOrderNumber,
                LogoType=excluded.LogoType,
                SupplyVoltage=excluded.SupplyVoltage,
                CycleInput=excluded.CycleInput,
                CycleSignal=excluded.CycleSignal,
                ValveOutput=excluded.ValveOutput,
                ValveVoltage=excluded.ValveVoltage,
                UseInterfaceRelay=excluded.UseInterfaceRelay,
                EndPositionMonitoring=excluded.EndPositionMonitoring,
                EndPositionInput=excluded.EndPositionInput,
                DefaultValvePulseMs=excluded.DefaultValvePulseMs,
                ReleaseState=excluded.ReleaseState,
                Notes=excluded.Notes,
                UpdatedAtUtc=excluded.UpdatedAtUtc;
            """;
        command.Parameters.AddWithValue("$machine", profile.MachineNumber);
        command.Parameters.AddWithValue("$orderNumber", profile.LogoOrderNumber);
        command.Parameters.AddWithValue("$logoType", profile.LogoType);
        command.Parameters.AddWithValue("$supply", profile.SupplyVoltage);
        command.Parameters.AddWithValue("$cycleInput", profile.CycleInput);
        command.Parameters.AddWithValue("$cycleSignal", profile.CycleSignal);
        command.Parameters.AddWithValue("$valveOutput", profile.ValveOutput);
        command.Parameters.AddWithValue("$valveVoltage", profile.ValveVoltage);
        command.Parameters.AddWithValue("$relay", profile.UseInterfaceRelay ? 1 : 0);
        command.Parameters.AddWithValue("$endMonitoring", profile.EndPositionMonitoring ? 1 : 0);
        command.Parameters.AddWithValue("$endInput", profile.EndPositionInput);
        command.Parameters.AddWithValue("$pulse", (int)profile.DefaultValvePulseMs);
        command.Parameters.AddWithValue("$release", (int)profile.ReleaseState);
        command.Parameters.AddWithValue("$notes", profile.Notes);
        command.Parameters.AddWithValue("$updated", profile.UpdatedAtUtc.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<CommissioningCheckRecord>> LoadChecksAsync(int machineNumber)
    {
        var result = new List<CommissioningCheckRecord>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT MachineNumber, CheckCode, Result, Note, CheckedAtUtc
            FROM CommissioningChecks
            WHERE MachineNumber=$machine
            ORDER BY CheckCode;
            """;
        command.Parameters.AddWithValue("$machine", machineNumber);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new CommissioningCheckRecord(
                reader.GetInt32(0),
                reader.GetString(1),
                (CommissioningCheckResult)reader.GetInt32(2),
                reader.GetString(3),
                reader.IsDBNull(4)
                    ? null
                    : DateTime.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind)));
        }

        return result;
    }

    public async Task UpsertCheckAsync(CommissioningCheckRecord record)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO CommissioningChecks(MachineNumber, CheckCode, Result, Note, CheckedAtUtc)
            VALUES($machine, $code, $result, $note, $checked)
            ON CONFLICT(MachineNumber, CheckCode) DO UPDATE SET
                Result=excluded.Result,
                Note=excluded.Note,
                CheckedAtUtc=excluded.CheckedAtUtc;
            """;
        command.Parameters.AddWithValue("$machine", record.MachineNumber);
        command.Parameters.AddWithValue("$code", record.CheckCode);
        command.Parameters.AddWithValue("$result", (int)record.Result);
        command.Parameters.AddWithValue("$note", record.Note);
        command.Parameters.AddWithValue("$checked", record.CheckedAtUtc?.ToString("O") ?? (object)DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }
}
