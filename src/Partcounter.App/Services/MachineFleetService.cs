using System.Diagnostics;
using System.Collections.Concurrent;
using Partcounter.Models;

namespace Partcounter.Services;

public sealed record MachineSnapshotEventArgs(int MachineNumber, LogoSnapshot Snapshot);
public sealed record MachineConnectionEventArgs(int MachineNumber, ConnectionState State, string? Message = null);

public sealed class MachineFleetService : IAsyncDisposable
{
    private static readonly ConcurrentDictionary<int, MachineCommunicationDiagnostics> GlobalDiagnostics = new();

    private const int CommandAckTimeoutMs = 3_000;
    private const int CommandRetryCount = 3;
    private const int CommandRetryDelayMs = 150;

    private readonly Dictionary<int, Session> _sessions = new();
    private readonly List<Task> _workers = new();
    private CancellationTokenSource? _cts;

    public event EventHandler<MachineSnapshotEventArgs>? SnapshotReceived;
    public event EventHandler<MachineConnectionEventArgs>? ConnectionChanged;

    public static MachineCommunicationDiagnostics? GetGlobalCommunicationDiagnostics(int machineNumber) =>
        GlobalDiagnostics.TryGetValue(machineNumber, out var diagnostics) ? diagnostics : null;

    public async Task StartAsync(IEnumerable<MachineConfiguration> configurations)
    {
        await StopAsync();
        _cts = new CancellationTokenSource();

        foreach (var configuration in configurations.Where(c => c.Enabled))
        {
            var session = new Session(configuration);
            _sessions[configuration.MachineNumber] = session;
            UpdateGlobalDiagnostics(session);
            _workers.Add(Task.Run(() => PollLoopAsync(session, _cts.Token), _cts.Token));
        }
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;

        _cts.Cancel();
        try
        {
            await Task.WhenAll(_workers);
        }
        catch (OperationCanceledException)
        {
        }

        foreach (var session in _sessions.Values)
        {
            await session.Client.DisposeAsync();
            GlobalDiagnostics.TryRemove(session.Configuration.MachineNumber, out _);
        }

        _workers.Clear();
        _sessions.Clear();
        _cts.Dispose();
        _cts = null;
    }

    public MachineCommunicationDiagnostics? GetCommunicationDiagnostics(int machineNumber) =>
        _sessions.TryGetValue(machineNumber, out var session) ? BuildDiagnostics(session) : null;

    public async Task SendJobAsync(int machineNumber, JobParameters job, CancellationToken cancellationToken = default)
    {
        var session = GetSession(machineNumber);
        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureConnectedAsync(session, cancellationToken);
            await EnsureCommandSequenceSynchronizedAsync(session, cancellationToken);
            var sequence = session.NextCommandSequence();
            await ExecuteConfirmedCommandAsync(
                session,
                sequence,
                token => session.Client.WriteJobAsync(job, sequence, true, true, false, token),
                $"Auftrag an {session.Configuration.Name}",
                job.ActiveCavities,
                cancellationToken,
                job.HoldAfterVeNumber);
            UpdateGlobalDiagnostics(session);
        }
        finally
        {
            session.Gate.Release();
        }
    }

    public async Task UpdateVeTargetAsync(
        int machineNumber,
        JobParameters job,
        bool pauseCounting,
        CancellationToken cancellationToken = default)
    {
        var session = GetSession(machineNumber);
        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureConnectedAsync(session, cancellationToken);
            await EnsureCommandSequenceSynchronizedAsync(session, cancellationToken);
            var sequence = session.NextCommandSequence();
            await ExecuteConfirmedCommandAsync(
                session,
                sequence,
                token => session.Client.WriteJobAsync(job, sequence, true, false, pauseCounting, token),
                $"VE-Zielupdate an {session.Configuration.Name}",
                job.ActiveCavities,
                cancellationToken,
                job.HoldAfterVeNumber);
            UpdateGlobalDiagnostics(session);
        }
        finally
        {
            session.Gate.Release();
        }
    }

    public async Task PauseCountingAsync(int machineNumber, CancellationToken cancellationToken = default)
    {
        var session = GetSession(machineNumber);
        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureConnectedAsync(session, cancellationToken);
            await EnsureCommandSequenceSynchronizedAsync(session, cancellationToken);
            var sequence = session.NextCommandSequence();
            await ExecuteConfirmedCommandAsync(session, sequence,
                token => session.Client.SendCommandAsync(sequence, (ushort)(ModbusRegisterMap.CommandEnableAutomatic | ModbusRegisterMap.CommandPauseCounting), token),
                $"Zählpause an {session.Configuration.Name}", null, cancellationToken);
            UpdateGlobalDiagnostics(session);
        }
        finally
        {
            session.Gate.Release();
        }
    }

    public async Task ResumeCountingAsync(int machineNumber, CancellationToken cancellationToken = default)
    {
        var session = GetSession(machineNumber);
        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureConnectedAsync(session, cancellationToken);
            await EnsureCommandSequenceSynchronizedAsync(session, cancellationToken);
            var sequence = session.NextCommandSequence();
            await ExecuteConfirmedCommandAsync(session, sequence,
                token => session.Client.SendCommandAsync(sequence, ModbusRegisterMap.CommandEnableAutomatic, token),
                $"Zählfreigabe an {session.Configuration.Name}", null, cancellationToken);
            UpdateGlobalDiagnostics(session);
        }
        finally
        {
            session.Gate.Release();
        }
    }

    public async Task SetMachinePollingEnabledAsync(
        int machineNumber,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var session = GetSession(machineNumber);
        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            session.PollingEnabled = enabled;
            if (!enabled)
                session.Client.Disconnect();
            UpdateGlobalDiagnostics(session);
        }
        finally
        {
            session.Gate.Release();
        }
    }

    public async Task SendManualVeChangeAsync(int machineNumber, CancellationToken cancellationToken = default)
    {
        var session = GetSession(machineNumber);
        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureConnectedAsync(session, cancellationToken);
            await EnsureCommandSequenceSynchronizedAsync(session, cancellationToken);
            var sequence = session.NextCommandSequence();
            await ExecuteConfirmedCommandAsync(session, sequence,
                token => session.Client.SendCommandAsync(sequence, (ushort)(ModbusRegisterMap.CommandEnableAutomatic | ModbusRegisterMap.CommandManualVeChange), token),
                $"manueller VE-Wechsel an {session.Configuration.Name}", null, cancellationToken);
            UpdateGlobalDiagnostics(session);
        }
        finally
        {
            session.Gate.Release();
        }
    }

    public async Task ResetJobAsync(int machineNumber, CancellationToken cancellationToken = default)
    {
        var session = GetSession(machineNumber);
        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureConnectedAsync(session, cancellationToken);
            await EnsureCommandSequenceSynchronizedAsync(session, cancellationToken);
            var sequence = session.NextCommandSequence();
            await ExecuteConfirmedCommandAsync(session, sequence,
                token => session.Client.SendCommandAsync(sequence, (ushort)(ModbusRegisterMap.CommandEnableAutomatic | ModbusRegisterMap.CommandResetJob), token),
                $"Auftragsreset an {session.Configuration.Name}", null, cancellationToken);
            UpdateGlobalDiagnostics(session);
        }
        finally
        {
            session.Gate.Release();
        }
    }

    private async Task<LogoSnapshot> ExecuteConfirmedCommandAsync(
        Session session,
        ushort expectedSequence,
        Func<CancellationToken, Task> send,
        string operation,
        ushort? expectedCavities,
        CancellationToken cancellationToken,
        ushort? expectedHoldAfterVeNumber = null)
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
                    return ValidateAcknowledgement(session, beforeSend, expectedSequence, operation, expectedCavities, expectedHoldAfterVeNumber);

                await send(cancellationToken);
                return await WaitForCommandAcknowledgementAsync(session, expectedSequence, operation, expectedCavities, cancellationToken, expectedHoldAfterVeNumber);
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
        CancellationToken cancellationToken,
        ushort? expectedHoldAfterVeNumber = null)
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
                var validated = ValidateAcknowledgement(session, snapshot, expectedSequence, operation, expectedCavities, expectedHoldAfterVeNumber);
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
        ushort? expectedCavities,
        ushort? expectedHoldAfterVeNumber = null)
    {
        if (snapshot.AcknowledgedCommandSequence != expectedSequence)
            throw new InvalidOperationException($"{operation}: erwartete AckSequence {expectedSequence}, empfangen {snapshot.AcknowledgedCommandSequence}.");
        if (snapshot.ErrorCode != ModbusRegisterMap.ErrorNone)
            throw new InvalidOperationException($"{operation}: LOGO! hat den Befehl mit ErrorCode {snapshot.ErrorCode} abgelehnt.");
        if (expectedCavities.HasValue && snapshot.ActiveCavitiesEcho != expectedCavities.Value)
            throw new InvalidOperationException($"{operation}: Kavitäten-Echo {snapshot.ActiveCavitiesEcho} entspricht nicht Soll {expectedCavities.Value}.");
        if (expectedHoldAfterVeNumber.HasValue && snapshot.HoldAfterVeNumberEcho != expectedHoldAfterVeNumber.Value)
            throw new InvalidOperationException($"{operation}: HoldAfterVE-Echo {snapshot.HoldAfterVeNumberEcho} entspricht nicht Soll {expectedHoldAfterVeNumber.Value}.");
        session.LastSnapshot = snapshot;
        session.LastMessage = null;
        UpdateGlobalDiagnostics(session);
        return snapshot;
    }

    private async Task PollLoopAsync(Session session, CancellationToken cancellationToken)
    {
        await Task.Delay(session.Configuration.MachineNumber * 35, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            if (!session.PollingEnabled)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                continue;
            }

            try
            {
                await session.Gate.WaitAsync(cancellationToken);
                try
                {
                    await EnsureConnectedAsync(session, cancellationToken);
                    await session.Client.WriteHeartbeatAsync(session.NextHeartbeat(), cancellationToken);
                    var snapshot = await session.Client.ReadSnapshotAsync(cancellationToken);
                    session.LastSnapshot = snapshot;
                    session.LastMessage = null;
                    session.SynchronizeCommandSequence(snapshot.AcknowledgedCommandSequence);
                    UpdateGlobalDiagnostics(session);
                    SnapshotReceived?.Invoke(this, new MachineSnapshotEventArgs(session.Configuration.MachineNumber, snapshot));
                    PublishConnection(session, ConnectionState.Online, null);
                }
                finally
                {
                    session.Gate.Release();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                session.Client.Disconnect();
                session.LastMessage = ex.Message;
                PublishConnection(session, ConnectionState.Offline, ex.Message);
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task EnsureConnectedAsync(Session session, CancellationToken cancellationToken)
    {
        if (session.Client.IsConnected) return;
        await session.Client.ConnectAsync(cancellationToken);
    }

    private async Task EnsureCommandSequenceSynchronizedAsync(Session session, CancellationToken cancellationToken)
    {
        if (session.CommandSequenceSynchronized)
            return;

        var snapshot = await session.Client.ReadSnapshotAsync(cancellationToken);
        session.LastSnapshot = snapshot;
        session.LastMessage = null;
        session.SynchronizeCommandSequence(snapshot.AcknowledgedCommandSequence);
        UpdateGlobalDiagnostics(session);
        SnapshotReceived?.Invoke(this, new MachineSnapshotEventArgs(session.Configuration.MachineNumber, snapshot));
    }

    private Session GetSession(int machineNumber)
    {
        if (!_sessions.TryGetValue(machineNumber, out var session))
            throw new InvalidOperationException($"Maschine {machineNumber:00} ist im Echtbetrieb nicht initialisiert.");
        return session;
    }

    private void PublishConnection(Session session, ConnectionState state, string? message)
    {
        session.LastMessage = message;
        session.LastState = state;
        UpdateGlobalDiagnostics(session);
        if (state == ConnectionState.Online && session.ConnectionStatePublishedOnline)
            return;
        session.ConnectionStatePublishedOnline = state == ConnectionState.Online;
        ConnectionChanged?.Invoke(this, new MachineConnectionEventArgs(session.Configuration.MachineNumber, state, message));
    }

    private static void UpdateGlobalDiagnostics(Session session) =>
        GlobalDiagnostics[session.Configuration.MachineNumber] = BuildDiagnostics(session);

    private static MachineCommunicationDiagnostics BuildDiagnostics(Session session)
    {
        var snapshot = session.LastSnapshot;
        return new MachineCommunicationDiagnostics(
            session.Configuration.MachineNumber,
            true,
            session.PollingEnabled,
            session.LastState,
            session.Heartbeat,
            session.CommandSequence,
            session.CommandSequenceSynchronized,
            snapshot?.AcknowledgedCommandSequence ?? 0,
            snapshot?.LogoHeartbeat ?? 0,
            snapshot?.StatusWord ?? 0,
            snapshot?.ErrorCode ?? 0,
            snapshot?.CompletionSequence ?? 0,
            snapshot?.ActiveCavitiesEcho ?? 0,
            snapshot?.CurrentParts ?? 0,
            snapshot?.TotalCycles ?? 0,
            snapshot?.CurrentVeNumber ?? 0,
            snapshot?.CompletedVes ?? 0,
            snapshot?.ReadAtUtc,
            session.LastMessage);
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private sealed class Session
    {
        private ushort _commandSequence;
        private ushort _heartbeat;
        private bool _commandSequenceSynchronized;

        public Session(MachineConfiguration configuration)
        {
            Configuration = configuration;
            Client = new LogoModbusClient(configuration);
        }

        public MachineConfiguration Configuration { get; }
        public LogoModbusClient Client { get; }
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public bool PollingEnabled { get; set; } = true;
        public ConnectionState LastState { get; set; } = ConnectionState.Offline;
        public bool ConnectionStatePublishedOnline { get; set; }
        public LogoSnapshot? LastSnapshot { get; set; }
        public string? LastMessage { get; set; }
        public bool CommandSequenceSynchronized => _commandSequenceSynchronized;
        public ushort CommandSequence => _commandSequence;
        public ushort Heartbeat => _heartbeat;

        public void SynchronizeCommandSequence(ushort acknowledgedSequence)
        {
            if (_commandSequenceSynchronized)
                return;

            if (acknowledgedSequence > ModbusRegisterMap.MaxSequenceValue)
                throw new InvalidOperationException($"LOGO! AckSequence {acknowledgedSequence} is outside the Partcounter V3 range.");

            _commandSequence = acknowledgedSequence;
            _commandSequenceSynchronized = true;
        }

        public ushort NextCommandSequence()
        {
            if (!_commandSequenceSynchronized)
                throw new InvalidOperationException("Command sequence has not been synchronized with the LOGO! yet.");

            _commandSequence = _commandSequence >= ModbusRegisterMap.MaxSequenceValue
                ? (ushort)1
                : (ushort)(_commandSequence + 1);
            return _commandSequence;
        }

        public ushort NextHeartbeat()
        {
            _heartbeat = _heartbeat >= ModbusRegisterMap.MaxHeartbeatValue
                ? (ushort)1
                : (ushort)(_heartbeat + 1);
            return _heartbeat;
        }
    }
}
