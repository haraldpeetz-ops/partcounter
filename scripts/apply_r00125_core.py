from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]


def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")


def write(rel, text):
    p = ROOT / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(text, encoding="utf-8", newline="\n")


def replace_once(rel, old, new):
    text = read(rel)
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{rel}: expected 1 occurrence, got {count}: {old[:100]!r}")
    write(rel, text.replace(old, new, 1))


# 1) .NET 10 LTS, version and warning gate
csproj = "src/Partcounter.App/Partcounter.App.csproj"
text = read(csproj)
replacements = {
    "<TargetFramework>net8.0-windows</TargetFramework>": "<TargetFramework>net10.0-windows</TargetFramework>",
    "<Version>0.1.24</Version>": "<Version>0.1.25</Version>",
    "<FileVersion>0.1.24.0</FileVersion>": "<FileVersion>0.1.25.0</FileVersion>",
    "<InformationalVersion>0.1.24-r001.24-adaptive-ui-logo-guide</InformationalVersion>": "<InformationalVersion>0.1.25-r001.25-final-hardening</InformationalVersion>",
    'Microsoft.Data.Sqlite\" Version=\"8.0.30\"': 'Microsoft.Data.Sqlite\" Version=\"10.0.0\"',
    'System.Text.Encoding.CodePages\" Version=\"8.0.0\"': 'System.Text.Encoding.CodePages\" Version=\"10.0.0\"',
    'System.Security.Cryptography.ProtectedData\" Version=\"8.0.0\"': 'System.Security.Cryptography.ProtectedData\" Version=\"10.0.0\"',
    'Help\\PARTCOUNTER_HILFE_R001_19.md': 'Help\\PARTCOUNTER_HILFE_R001_25.md',
}
for old, new in replacements.items():
    if old not in text:
        raise RuntimeError(f"csproj replacement missing: {old}")
    text = text.replace(old, new, 1)
if "<TreatWarningsAsErrors>" not in text:
    text = text.replace("<Nullable>enable</Nullable>", "<Nullable>enable</Nullable>\n    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>", 1)
write(csproj, text)

# Explicitly discard fire-and-forget DispatcherOperation results; removes CS4014 without changing behavior.
for p in (ROOT / "src/Partcounter.App").rglob("*.cs"):
    lines = p.read_text(encoding="utf-8").splitlines(True)
    out = []
    changed = False
    for line in lines:
        stripped = line.lstrip()
        if "BeginInvoke(" in stripped and not stripped.startswith(("_ =", "await ", "return ", "var ")):
            if re.match(r'^(?:[A-Za-z_][A-Za-z0-9_?.]*\.)?Dispatcher\.BeginInvoke\(', stripped) or re.match(r'^[A-Za-z_][A-Za-z0-9_?.]*\.Dispatcher\.BeginInvoke\(', stripped):
                indent = line[:len(line)-len(stripped)]
                line = indent + "_ = " + stripped
                changed = True
        out.append(line)
    if changed:
        p.write_text("".join(out), encoding="utf-8", newline="\n")

# 2) One process-wide SQLite writer coordinator
write("src/Partcounter.App/Services/SqliteWriteCoordinator.cs", '''using Microsoft.Data.Sqlite;

namespace Partcounter.Services;

/// <summary>
/// Process-wide SQLite writer serialization. WAL permits concurrent readers, but SQLite still
/// permits only one writer. All production-relevant writers use this gate in R001.25.
/// </summary>
public static class SqliteWriteCoordinator
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    public const int BusyTimeoutSeconds = 15;

    public static string BuildConnectionString(string databasePath) =>
        $"Data Source={databasePath};Cache=Shared;Default Timeout={BusyTimeoutSeconds};Pooling=True";

    public static async Task ExecuteAsync(
        string databasePath,
        Func<SqliteConnection, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentNullException.ThrowIfNull(action);
        await Gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(BuildConnectionString(databasePath));
            await connection.OpenAsync(cancellationToken);
            await ConfigureAsync(connection, cancellationToken);
            await action(connection);
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task<T> ExecuteAsync<T>(
        string databasePath,
        Func<SqliteConnection, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentNullException.ThrowIfNull(action);
        await Gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(BuildConnectionString(databasePath));
            await connection.OpenAsync(cancellationToken);
            await ConfigureAsync(connection, cancellationToken);
            return await action(connection);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task ConfigureAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA busy_timeout={BusyTimeoutSeconds * 1000}; PRAGMA foreign_keys=ON;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
''')

rel = "src/Partcounter.App/Services/DatabaseService.cs"
text = read(rel)
text = text.replace("    private static readonly SemaphoreSlim WriteGate = new(1, 1);\n", "", 1)
text = text.replace('    private string ConnectionString => $"Data Source={DatabasePath};Cache=Shared;Default Timeout=15;Pooling=True";', '    private string ConnectionString => SqliteWriteCoordinator.BuildConnectionString(DatabasePath);', 1)
old = '''    private async Task ExecuteWriteAsync(Func<SqliteConnection, Task> write)
    {
        await WriteGate.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();
            await using (var busy = connection.CreateCommand())
            {
                busy.CommandText = "PRAGMA busy_timeout=15000; PRAGMA foreign_keys=ON;";
                await busy.ExecuteNonQueryAsync();
            }
            await write(connection);
        }
        finally
        {
            WriteGate.Release();
        }
    }'''
new = '''    private Task ExecuteWriteAsync(Func<SqliteConnection, Task> write) =>
        SqliteWriteCoordinator.ExecuteAsync(DatabasePath, write);

    public Task ExecuteExclusiveWriteAsync(Func<SqliteConnection, Task> write, CancellationToken cancellationToken = default) =>
        SqliteWriteCoordinator.ExecuteAsync(DatabasePath, write, cancellationToken);

    public Task<T> ExecuteExclusiveWriteAsync<T>(Func<SqliteConnection, Task<T>> write, CancellationToken cancellationToken = default) =>
        SqliteWriteCoordinator.ExecuteAsync(DatabasePath, write, cancellationToken);'''
if old not in text:
    raise RuntimeError("DatabaseService writer block not found")
write(rel, text.replace(old, new, 1))

rel = "src/Partcounter.App/ViewModels/MachineSetupViewModel.cs"
old = '''            await using var connection = new SqliteConnection($"Data Source={_database.DatabasePath};Cache=Shared");
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            foreach (var machine in Machines)
            {
                var command = connection.CreateCommand();
                command.Transaction = (SqliteTransaction)transaction;
                command.CommandText = """
                    UPDATE Machines
                    SET Name=$name, IpAddress=$ip, Port=$port, UnitId=$unit, Enabled=$enabled
                    WHERE MachineNumber=$number;
                    """;
                command.Parameters.AddWithValue("$name", machine.Name.Trim());
                command.Parameters.AddWithValue("$ip", machine.IpAddress.Trim());
                command.Parameters.AddWithValue("$port", machine.Port);
                command.Parameters.AddWithValue("$unit", machine.UnitId);
                command.Parameters.AddWithValue("$enabled", machine.Enabled ? 1 : 0);
                command.Parameters.AddWithValue("$number", machine.MachineNumber);
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();'''
new = '''            await _database.ExecuteExclusiveWriteAsync(async connection =>
            {
                await using var transaction = await connection.BeginTransactionAsync();
                foreach (var machine in Machines)
                {
                    var command = connection.CreateCommand();
                    command.Transaction = (SqliteTransaction)transaction;
                    command.CommandText = """
                        UPDATE Machines
                        SET Name=$name, IpAddress=$ip, Port=$port, UnitId=$unit, Enabled=$enabled
                        WHERE MachineNumber=$number;
                        """;
                    command.Parameters.AddWithValue("$name", machine.Name.Trim());
                    command.Parameters.AddWithValue("$ip", machine.IpAddress.Trim());
                    command.Parameters.AddWithValue("$port", machine.Port);
                    command.Parameters.AddWithValue("$unit", machine.UnitId);
                    command.Parameters.AddWithValue("$enabled", machine.Enabled ? 1 : 0);
                    command.Parameters.AddWithValue("$number", machine.MachineNumber);
                    await command.ExecuteNonQueryAsync();
                }
                await transaction.CommitAsync();
            });'''
replace_once(rel, old, new)

# Label template save/delete are serialized globally.
rel = "src/Partcounter.App/Services/LabelTemplateService.cs"
text = read(rel)
text = text.replace('    private string ConnectionString => $"Data Source={_databasePath};Cache=Shared";', '    private string ConnectionString => SqliteWriteCoordinator.BuildConnectionString(_databasePath);', 1)
start_old = '''        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        if (template.IsDefault)'''
start_new = '''        await SqliteWriteCoordinator.ExecuteAsync(_databasePath, async connection =>
        {
            await using var transaction = await connection.BeginTransactionAsync();

            if (template.IsDefault)'''
if start_old not in text:
    raise RuntimeError("LabelTemplate Save start not found")
text = text.replace(start_old, start_new, 1)
end_old = '''        await transaction.CommitAsync();
    }

    public async Task DeleteTemplateAsync'''
end_new = '''            await transaction.CommitAsync();
        });
    }

    public async Task DeleteTemplateAsync'''
if end_old not in text:
    raise RuntimeError("LabelTemplate Save end not found")
text = text.replace(end_old, end_new, 1)
delete_old = '''        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM LabelTemplates WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();'''
delete_new = '''        await SqliteWriteCoordinator.ExecuteAsync(_databasePath, async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM LabelTemplates WHERE Id=$id;";
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync();
        });'''
if delete_old not in text:
    raise RuntimeError("LabelTemplate delete not found")
write(rel, text.replace(delete_old, delete_new, 1))

# Reprint numbering and journal insert become one serialized atomic operation.
rel = "src/Partcounter.App/Services/LabelReprintService.cs"
text = read(rel)
text = text.replace('    private string ConnectionString => $"Data Source={_database.DatabasePath};Cache=Shared";', '    private string ConnectionString => SqliteWriteCoordinator.BuildConnectionString(_database.DatabasePath);', 1)
text = text.replace("        var reprintNumber = await GetNextReprintNumberAsync(record.Id);\n", "        var reprintNumber = 0;\n", 1)
old = '''        await AddJournalEntryAsync(new LabelReprintJournalEntry(
            0,
            record.Id,
            reprintNumber,
            attemptedAtUtc,
            normalizedPrinter,
            normalizedReason,
            successful,
            errorMessage,
            layoutSource));'''
new = '''        reprintNumber = await AddJournalEntryAsync(
            record.Id,
            attemptedAtUtc,
            normalizedPrinter,
            normalizedReason,
            successful,
            errorMessage,
            layoutSource);'''
if old not in text:
    raise RuntimeError("Reprint call not found")
text = text.replace(old, new, 1)
pattern = re.compile(r'    private async Task AddJournalEntryAsync\(LabelReprintJournalEntry entry\)\n    \{.*?\n    \}\n\n    private static async Task EnsureColumnAsync', re.S)
replacement = '''    private Task<int> AddJournalEntryAsync(
        string packagingUnitId,
        DateTime attemptedAtUtc,
        string printerName,
        string reason,
        bool successful,
        string errorMessage,
        string layoutSource) =>
        _database.ExecuteExclusiveWriteAsync(async connection =>
        {
            var next = connection.CreateCommand();
            next.CommandText = "SELECT COALESCE(MAX(ReprintNumber), 0) + 1 FROM LabelReprintJournal WHERE PackagingUnitId=$id;";
            next.Parameters.AddWithValue("$id", packagingUnitId);
            var reprintNumber = Convert.ToInt32(await next.ExecuteScalarAsync() ?? 1);

            var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO LabelReprintJournal
                    (PackagingUnitId, ReprintNumber, PrintedAtUtc, PrinterName, Reason, Successful, ErrorMessage, LayoutSource)
                VALUES
                    ($id, $number, $time, $printer, $reason, $successful, $error, $layout);
                """;
            command.Parameters.AddWithValue("$id", packagingUnitId);
            command.Parameters.AddWithValue("$number", reprintNumber);
            command.Parameters.AddWithValue("$time", attemptedAtUtc.ToString("O"));
            command.Parameters.AddWithValue("$printer", printerName);
            command.Parameters.AddWithValue("$reason", reason);
            command.Parameters.AddWithValue("$successful", successful ? 1 : 0);
            command.Parameters.AddWithValue("$error", errorMessage ?? string.Empty);
            command.Parameters.AddWithValue("$layout", layoutSource ?? string.Empty);
            await command.ExecuteNonQueryAsync();
            return reprintNumber;
        });

    private static async Task EnsureColumnAsync'''
text, count = pattern.subn(replacement, text, count=1)
if count != 1:
    raise RuntimeError("Reprint AddJournal method not found")
text = text.replace('''            CREATE INDEX IF NOT EXISTS IX_LabelReprintJournal_PackagingUnitId
                ON LabelReprintJournal(PackagingUnitId, Id DESC);''', '''            CREATE INDEX IF NOT EXISTS IX_LabelReprintJournal_PackagingUnitId
                ON LabelReprintJournal(PackagingUnitId, Id DESC);

            CREATE UNIQUE INDEX IF NOT EXISTS UX_LabelReprintJournal_Number
                ON LabelReprintJournal(PackagingUnitId, ReprintNumber);''', 1)
write(rel, text)

# 3) Confirmed Modbus command/Ack with same-sequence retry.
rel = "src/Partcounter.App/Services/MachineFleetService.cs"
text = read(rel)
if "using System.Diagnostics;" not in text:
    text = "using System.Diagnostics;\n" + text
text = text.replace('''    private readonly Dictionary<int, Session> _sessions = new();
    private readonly List<Task> _workers = new();''', '''    private const int CommandAckTimeoutMs = 3_000;
    private const int CommandRetryCount = 3;
    private const int CommandRetryDelayMs = 150;

    private readonly Dictionary<int, Session> _sessions = new();
    private readonly List<Task> _workers = new();''', 1)

old = '''            var sequence = session.NextCommandSequence();
            await session.Client.WriteJobAsync(
                job,
                sequence,
                automaticMode: true,
                resetJob: true,
                pauseCounting: false,
                cancellationToken: cancellationToken);
            UpdateGlobalDiagnostics(session);'''
new = '''            var sequence = session.NextCommandSequence();
            await ExecuteConfirmedCommandAsync(
                session,
                sequence,
                token => session.Client.WriteJobAsync(job, sequence, true, true, false, token),
                $"Auftrag an {session.Configuration.Name}",
                job.ActiveCavities,
                cancellationToken);
            UpdateGlobalDiagnostics(session);'''
if old not in text: raise RuntimeError("SendJob block missing")
text = text.replace(old, new, 1)

old = '''            var sequence = session.NextCommandSequence();
            await session.Client.WriteJobAsync(
                job,
                sequence,
                automaticMode: true,
                resetJob: false,
                pauseCounting: pauseCounting,
                cancellationToken: cancellationToken);
            UpdateGlobalDiagnostics(session);'''
new = '''            var sequence = session.NextCommandSequence();
            await ExecuteConfirmedCommandAsync(
                session,
                sequence,
                token => session.Client.WriteJobAsync(job, sequence, true, false, pauseCounting, token),
                $"VE-Zielupdate an {session.Configuration.Name}",
                job.ActiveCavities,
                cancellationToken);
            UpdateGlobalDiagnostics(session);'''
if old not in text: raise RuntimeError("Update target block missing")
text = text.replace(old, new, 1)

pairs = [
('''            await session.Client.SendCommandAsync(
                session.NextCommandSequence(),
                (ushort)(ModbusRegisterMap.CommandEnableAutomatic | ModbusRegisterMap.CommandPauseCounting),
                cancellationToken);
            UpdateGlobalDiagnostics(session);''', '''            var sequence = session.NextCommandSequence();
            await ExecuteConfirmedCommandAsync(session, sequence,
                token => session.Client.SendCommandAsync(sequence, (ushort)(ModbusRegisterMap.CommandEnableAutomatic | ModbusRegisterMap.CommandPauseCounting), token),
                $"Zählpause an {session.Configuration.Name}", null, cancellationToken);
            UpdateGlobalDiagnostics(session);'''),
('''            await session.Client.SendCommandAsync(
                session.NextCommandSequence(),
                ModbusRegisterMap.CommandEnableAutomatic,
                cancellationToken);
            UpdateGlobalDiagnostics(session);''', '''            var sequence = session.NextCommandSequence();
            await ExecuteConfirmedCommandAsync(session, sequence,
                token => session.Client.SendCommandAsync(sequence, ModbusRegisterMap.CommandEnableAutomatic, token),
                $"Zählfreigabe an {session.Configuration.Name}", null, cancellationToken);
            UpdateGlobalDiagnostics(session);'''),
('''            await session.Client.SendCommandAsync(
                session.NextCommandSequence(),
                (ushort)(ModbusRegisterMap.CommandEnableAutomatic | ModbusRegisterMap.CommandManualVeChange),
                cancellationToken);
            UpdateGlobalDiagnostics(session);''', '''            var sequence = session.NextCommandSequence();
            await ExecuteConfirmedCommandAsync(session, sequence,
                token => session.Client.SendCommandAsync(sequence, (ushort)(ModbusRegisterMap.CommandEnableAutomatic | ModbusRegisterMap.CommandManualVeChange), token),
                $"manueller VE-Wechsel an {session.Configuration.Name}", null, cancellationToken);
            UpdateGlobalDiagnostics(session);'''),
('''            await session.Client.SendCommandAsync(
                session.NextCommandSequence(),
                (ushort)(ModbusRegisterMap.CommandEnableAutomatic | ModbusRegisterMap.CommandResetJob),
                cancellationToken);
            UpdateGlobalDiagnostics(session);''', '''            var sequence = session.NextCommandSequence();
            await ExecuteConfirmedCommandAsync(session, sequence,
                token => session.Client.SendCommandAsync(sequence, (ushort)(ModbusRegisterMap.CommandEnableAutomatic | ModbusRegisterMap.CommandResetJob), token),
                $"Auftragsreset an {session.Configuration.Name}", null, cancellationToken);
            UpdateGlobalDiagnostics(session);''')]
for old, new in pairs:
    if old not in text: raise RuntimeError("Inline command block missing")
    text = text.replace(old, new, 1)

helper = '''    private async Task<LogoSnapshot> ExecuteConfirmedCommandAsync(
        Session session,
        ushort expectedSequence,
        Func<CancellationToken, Task> send,
        string operation,
        ushort? expectedCavities,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (var attempt = 1; attempt <= CommandRetryCount; attempt++)
        {
            try
            {
                await EnsureConnectedAsync(session, cancellationToken);
                var beforeSend = await session.Client.ReadSnapshotAsync(cancellationToken);
                session.LastSnapshot = beforeSend;
                if (beforeSend.AcknowledgedCommandSequence == expectedSequence)
                    return ValidateAcknowledgement(session, beforeSend, expectedSequence, operation, expectedCavities);

                await send(cancellationToken);
                return await WaitForCommandAcknowledgementAsync(session, expectedSequence, operation, expectedCavities, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                session.LastMessage = $"{operation}: Versuch {attempt}/{CommandRetryCount} fehlgeschlagen: {ex.Message}";
                session.Client.Disconnect();
                PublishConnection(session, ConnectionState.Offline, session.LastMessage);
                if (attempt < CommandRetryCount)
                    await Task.Delay(CommandRetryDelayMs * attempt, cancellationToken);
            }
        }
        throw new InvalidOperationException($"{operation} wurde von der LOGO! nach {CommandRetryCount} Versuchen nicht bestätigt.", lastError);
    }

    private async Task<LogoSnapshot> WaitForCommandAcknowledgementAsync(
        Session session,
        ushort expectedSequence,
        string operation,
        ushort? expectedCavities,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < CommandAckTimeoutMs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await session.Client.ReadSnapshotAsync(cancellationToken);
            session.LastSnapshot = snapshot;
            session.LastMessage = null;
            UpdateGlobalDiagnostics(session);
            if (snapshot.AcknowledgedCommandSequence == expectedSequence)
            {
                var validated = ValidateAcknowledgement(session, snapshot, expectedSequence, operation, expectedCavities);
                PublishConnection(session, ConnectionState.Online, null);
                return validated;
            }
            await Task.Delay(75, cancellationToken);
        }
        throw new TimeoutException($"{operation}: LOGO!-AckSequence {expectedSequence} wurde innerhalb von {CommandAckTimeoutMs} ms nicht bestätigt.");
    }

    private static LogoSnapshot ValidateAcknowledgement(
        Session session,
        LogoSnapshot snapshot,
        ushort expectedSequence,
        string operation,
        ushort? expectedCavities)
    {
        if (snapshot.AcknowledgedCommandSequence != expectedSequence)
            throw new InvalidOperationException($"{operation}: erwartete AckSequence {expectedSequence}, empfangen {snapshot.AcknowledgedCommandSequence}.");
        if (snapshot.ErrorCode != ModbusRegisterMap.ErrorNone)
            throw new InvalidOperationException($"{operation}: LOGO! hat den Befehl mit ErrorCode {snapshot.ErrorCode} abgelehnt.");
        if (expectedCavities.HasValue && snapshot.ActiveCavitiesEcho != expectedCavities.Value)
            throw new InvalidOperationException($"{operation}: Kavitäten-Echo {snapshot.ActiveCavitiesEcho} entspricht nicht Soll {expectedCavities.Value}.");
        session.LastSnapshot = snapshot;
        session.LastMessage = null;
        UpdateGlobalDiagnostics(session);
        return snapshot;
    }

'''
marker = "    private async Task PollLoopAsync(Session session, CancellationToken cancellationToken)\n"
if marker not in text: raise RuntimeError("PollLoop marker missing")
text = text.replace(marker, helper + marker, 1)
text = text.replace("await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken);", "await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);")
write(rel, text)

# 4) Central version and confirmed VE-boundary transaction.
rel = "src/Partcounter.App/ViewModels/MainViewModel.cs"
text = read(rel)
old = '    public string SystemStatusText => IsSimulationMode ? "R001.5 · SIMULATION" : "R001.5 · ECHTBETRIEB MODBUS TCP";'
new = '    public string SystemStatusText => IsSimulationMode ? AppVersionInfo.SimulationStatus : AppVersionInfo.ProductionStatus;'
if old not in text: raise RuntimeError("stale system status string missing")
text = text.replace(old, new, 1)
old = '''                    await _fleet.UpdateVeTargetAsync(
                        machine.Configuration.MachineNumber,
                        nextJob,
                        machine.OrderState == ProductionOrderState.Paused);'''
new = '''                    await _fleet.PauseCountingAsync(machine.Configuration.MachineNumber);
                    await _fleet.UpdateVeTargetAsync(
                        machine.Configuration.MachineNumber,
                        nextJob,
                        pauseCounting: true);
                    if (machine.OrderState == ProductionOrderState.Running)
                        await _fleet.ResumeCountingAsync(machine.Configuration.MachineNumber);'''
if old not in text: raise RuntimeError("next VE update block missing")
write(rel, text.replace(old, new, 1))

# 5) Real 24/7 backup scheduler, not only once at application start.
rel = "src/Partcounter.App/Services/ProductionReadinessBootstrap.cs"
text = read(rel)
text = text.replace("    private DispatcherTimer? _settingsScrollTimer;", "    private DispatcherTimer? _settingsScrollTimer;\n    private DispatcherTimer? _dailyBackupTimer;\n    private bool _dailyBackupCheckRunning;", 1)
needle = "            var automaticBackup = await _service.EnsureDailyBackupAsync();\n"
if needle not in text: raise RuntimeError("automatic backup call missing")
text = text.replace(needle, needle + "            StartDailyBackupTimer();\n", 1)
old = '''        if (_settingsScrollTimer is not null)
        {
            _settingsScrollTimer.Stop();
            _settingsScrollTimer = null;
        }'''
new = '''        if (_settingsScrollTimer is not null)
        {
            _settingsScrollTimer.Stop();
            _settingsScrollTimer = null;
        }
        if (_dailyBackupTimer is not null)
        {
            _dailyBackupTimer.Stop();
            _dailyBackupTimer = null;
        }'''
if old not in text: raise RuntimeError("settings timer close block missing")
text = text.replace(old, new, 1)
methods = '''    private void StartDailyBackupTimer()
    {
        _dailyBackupTimer ??= new DispatcherTimer(
            TimeSpan.FromMinutes(30),
            DispatcherPriority.Background,
            async (_, _) => await CheckDailyBackupAsync(),
            _window.Dispatcher);
        _dailyBackupTimer.Start();
    }

    private async Task CheckDailyBackupAsync()
    {
        if (_dailyBackupCheckRunning) return;
        _dailyBackupCheckRunning = true;
        try
        {
            var created = await _service.EnsureDailyBackupAsync();
            RefreshLastBackupText();
            if (created is not null && _statusText is not null)
                _statusText.Text = $"Automatische Tagessicherung erstellt und geprüft: {created}";
        }
        catch (Exception ex)
        {
            if (_statusText is not null)
                _statusText.Text = $"Automatische Tagessicherung fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            _dailyBackupCheckRunning = false;
        }
    }

'''
marker = "    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)\n"
if marker not in text: raise RuntimeError("ProductionReadiness insertion marker missing")
write(rel, text.replace(marker, methods + marker, 1))

# 6) Exactly one productive order source.
write("src/Partcounter.App/Services/OrderSourceCoordinator.cs", '''namespace Partcounter.Services;

public enum OrderSourceKind { ArburgAls, ProAlpha }

public static class OrderSourceCoordinator
{
    public const string ActiveSourceSettingKey = "OrderSource.Active";
    public const string AlsDisplayName = "ARBURG ALS";
    public const string ProAlphaDisplayName = "proALPHA";

    public static async Task<OrderSourceKind> GetActiveAsync(DatabaseService database)
    {
        var value = await database.GetSettingAsync(ActiveSourceSettingKey);
        return string.Equals(value, ProAlphaDisplayName, StringComparison.OrdinalIgnoreCase)
            ? OrderSourceKind.ProAlpha : OrderSourceKind.ArburgAls;
    }

    public static Task SetActiveAsync(DatabaseService database, OrderSourceKind source) =>
        database.SetSettingAsync(ActiveSourceSettingKey, source == OrderSourceKind.ProAlpha ? ProAlphaDisplayName : AlsDisplayName);

    public static async Task<bool> IsActiveAsync(DatabaseService database, OrderSourceKind source) =>
        await GetActiveAsync(database) == source;
}
''')

rel = "src/Partcounter.App/Services/OrderSourceHubBootstrap.cs"
text = read(rel)
text = text.replace('    private const string ActiveSourceSettingKey = "OrderSource.Active";\n    private const string AlsSource = "ARBURG ALS";\n    private const string ProAlphaSource = "proALPHA";', '    private const string AlsSource = OrderSourceCoordinator.AlsDisplayName;\n    private const string ProAlphaSource = OrderSourceCoordinator.ProAlphaDisplayName;', 1)
old = "        var active = await _database.GetSettingAsync(ActiveSourceSettingKey);\n        active = string.Equals(active, ProAlphaSource, StringComparison.OrdinalIgnoreCase) ? ProAlphaSource : AlsSource;"
new = "        var activeKind = await OrderSourceCoordinator.GetActiveAsync(_database);\n        var active = activeKind == OrderSourceKind.ProAlpha ? ProAlphaSource : AlsSource;"
if old not in text: raise RuntimeError("Order source load block missing")
text = text.replace(old, new, 1)
old = "            await _database.SetSettingAsync(ActiveSourceSettingKey, selected);"
new = "            await OrderSourceCoordinator.SetActiveAsync(_database, selected == ProAlphaSource ? OrderSourceKind.ProAlpha : OrderSourceKind.ArburgAls);"
if old not in text: raise RuntimeError("Order source save block missing")
write(rel, text.replace(old, new, 1))

rel = "src/Partcounter.App/ViewModels/AlsViewModel.cs"
text = read(rel)
old = '''    private async Task LoadOrdersAsync(bool userInitiated)
    {
        if (_isLoading) return;'''
new = '''    private async Task LoadOrdersAsync(bool userInitiated)
    {
        if (_isLoading) return;
        if (!userInitiated && !await OrderSourceCoordinator.IsActiveAsync(_database, OrderSourceKind.ArburgAls)) return;'''
if old not in text: raise RuntimeError("ALS load marker missing")
text = text.replace(old, new, 1)
old = '''    private async Task ApplySelectedOrderAsync()
    {
        var order = SelectedOrder;'''
new = '''    private async Task ApplySelectedOrderAsync()
    {
        if (!await OrderSourceCoordinator.IsActiveAsync(_database, OrderSourceKind.ArburgAls))
        {
            StatusText = "ARBURG ALS ist nicht die führende Auftragsquelle. Unter Administration → Auftragsquellen zuerst ALS aktivieren.";
            return;
        }
        var order = SelectedOrder;'''
if old not in text: raise RuntimeError("ALS apply marker missing")
write(rel, text.replace(old, new, 1))

rel = "src/Partcounter.App/ViewModels/ProAlphaViewModel.cs"
text = read(rel)
old = '''    private async Task LoadOrdersAsync(bool userInitiated)
    {
        if (_isLoading || !Settings.Enabled) return;'''
new = '''    private async Task LoadOrdersAsync(bool userInitiated)
    {
        if (_isLoading || !Settings.Enabled) return;
        if (!userInitiated && !await OrderSourceCoordinator.IsActiveAsync(_database, OrderSourceKind.ProAlpha)) return;'''
if old not in text: raise RuntimeError("proALPHA load marker missing")
text = text.replace(old, new, 1)
old = '''    private async Task ApplySelectedOrderAsync()
    {
        var order = SelectedOrder;'''
new = '''    private async Task ApplySelectedOrderAsync()
    {
        if (!await OrderSourceCoordinator.IsActiveAsync(_database, OrderSourceKind.ProAlpha))
        {
            StatusText = "proALPHA ist nicht die führende Auftragsquelle. Unter Administration → Auftragsquellen zuerst proALPHA aktivieren.";
            return;
        }
        var order = SelectedOrder;'''
if old not in text: raise RuntimeError("proALPHA apply marker missing")
write(rel, text.replace(old, new, 1))

# 7) Update signing readiness: optional Authenticode enforcement controlled by manifest.
rel = "src/Partcounter.App/Services/PartcounterUpdateService.cs"
text = read(rel)
old = '''    public string ReleaseNotes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }'''
new = '''    public string ReleaseNotes { get; set; } = string.Empty;
    public bool RequireAuthenticode { get; set; }
    public string PublisherCertificateThumbprint { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }'''
if old not in text: raise RuntimeError("update manifest marker missing")
text = text.replace(old, new, 1)
old = '''        if (!File.Exists(Path.Combine(staging, "Partcounter.exe")))
            throw new InvalidDataException("Staging enthält keine Partcounter.exe.");'''
new = '''        var stagedExe = Path.Combine(staging, "Partcounter.exe");
        if (!File.Exists(stagedExe))
            throw new InvalidDataException("Staging enthält keine Partcounter.exe.");
        if (package.Manifest.RequireAuthenticode)
            VerifyAuthenticode(stagedExe, package.Manifest.PublisherCertificateThumbprint);'''
if old not in text: raise RuntimeError("update staging marker missing")
text = text.replace(old, new, 1)
method = '''    private static void VerifyAuthenticode(string executablePath, string expectedThumbprint)
    {
        var certificate = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(executablePath);
        using var certificate2 = new System.Security.Cryptography.X509Certificates.X509Certificate2(certificate);
        if (string.IsNullOrWhiteSpace(certificate2.Thumbprint))
            throw new InvalidDataException("Das Update verlangt Authenticode, aber Partcounter.exe besitzt kein Signaturzertifikat.");
        if (!string.IsNullOrWhiteSpace(expectedThumbprint))
        {
            var expected = expectedThumbprint.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
            var actual = certificate2.Thumbprint.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actual), Encoding.ASCII.GetBytes(expected)))
                throw new InvalidDataException("Das Signaturzertifikat des Updatepakets entspricht nicht dem freigegebenen Herausgeber.");
        }
    }

'''
marker = "    private static void EnsureInstallDirectoryWritable()\n"
if marker not in text: raise RuntimeError("update insertion marker missing")
write(rel, text.replace(marker, method + marker, 1))

print("R001.25 core hardening applied")
