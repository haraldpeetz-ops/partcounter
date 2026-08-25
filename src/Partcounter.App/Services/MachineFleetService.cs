using Partcounter.Models;

namespace Partcounter.Services;

public sealed record MachineSnapshotEventArgs(int MachineNumber, LogoSnapshot Snapshot);
public sealed record MachineConnectionEventArgs(int MachineNumber, ConnectionState State, string? Message = null);

public sealed class MachineFleetService : IAsyncDisposable
{
    private readonly Dictionary<int, Session> _sessions = new();
    private readonly List<Task> _workers = new();
    private CancellationTokenSource? _cts;

    public event EventHandler<MachineSnapshotEventArgs>? SnapshotReceived;
    public event EventHandler<MachineConnectionEventArgs>? ConnectionChanged;

    public async Task StartAsync(IEnumerable<MachineConfiguration> configurations)
    {
        await StopAsync();
        _cts = new CancellationTokenSource();

        foreach (var configuration in configurations.Where(c => c.Enabled))
        {
            var session = new Session(configuration);
            _sessions[configuration.MachineNumber] = session;
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
            await session.Client.DisposeAsync();

        _workers.Clear();
        _sessions.Clear();
        _cts.Dispose();
        _cts = null;
    }

    public async Task SendJobAsync(int machineNumber, JobParameters job, CancellationToken cancellationToken = default)
    {
        var session = GetSession(machineNumber);
        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureConnectedAsync(session, cancellationToken);
            var sequence = session.NextCommandSequence();
            await session.Client.WriteJobAsync(job, sequence, automaticMode: true, resetJob: true, cancellationToken);
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
            await session.Client.SendCommandAsync(
                session.NextCommandSequence(),
                (ushort)(ModbusRegisterMap.CommandEnableAutomatic | ModbusRegisterMap.CommandManualVeChange),
                cancellationToken);
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
            await session.Client.SendCommandAsync(
                session.NextCommandSequence(),
                (ushort)(ModbusRegisterMap.CommandEnableAutomatic | ModbusRegisterMap.CommandResetJob),
                cancellationToken);
        }
        finally
        {
            session.Gate.Release();
        }
    }

    private async Task PollLoopAsync(Session session, CancellationToken cancellationToken)
    {
        await Task.Delay(session.Configuration.MachineNumber * 35, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await session.Gate.WaitAsync(cancellationToken);
                try
                {
                    await EnsureConnectedAsync(session, cancellationToken);
                    session.Heartbeat++;
                    await session.Client.WriteHeartbeatAsync(session.Heartbeat, cancellationToken);
                    var snapshot = await session.Client.ReadSnapshotAsync(cancellationToken);
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
                PublishConnection(session, ConnectionState.Offline, ex.Message);
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken);
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

    private Session GetSession(int machineNumber)
    {
        if (!_sessions.TryGetValue(machineNumber, out var session))
            throw new InvalidOperationException($"Maschine {machineNumber:00} ist im Echtbetrieb nicht initialisiert.");
        return session;
    }

    private void PublishConnection(Session session, ConnectionState state, string? message)
    {
        if (session.LastState == state && state == ConnectionState.Online) return;
        session.LastState = state;
        ConnectionChanged?.Invoke(this, new MachineConnectionEventArgs(session.Configuration.MachineNumber, state, message));
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private sealed class Session
    {
        private ushort _commandSequence;

        public Session(MachineConfiguration configuration)
        {
            Configuration = configuration;
            Client = new LogoModbusClient(configuration);
        }

        public MachineConfiguration Configuration { get; }
        public LogoModbusClient Client { get; }
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public ushort Heartbeat { get; set; }
        public ConnectionState LastState { get; set; } = ConnectionState.Offline;

        public ushort NextCommandSequence()
        {
            _commandSequence++;
            if (_commandSequence == 0) _commandSequence = 1;
            return _commandSequence;
        }
    }
}
