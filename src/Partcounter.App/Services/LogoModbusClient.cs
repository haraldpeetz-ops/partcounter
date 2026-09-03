using System.Net.Sockets;
using NModbus;
using Partcounter.Models;

namespace Partcounter.Services;

public sealed class LogoModbusClient : IAsyncDisposable
{
    private const int ConnectTimeoutMs = 2_500;
    private const uint StaleResponseTransactionWindow = 16;

    private readonly MachineConfiguration _configuration;
    private readonly SemaphoreSlim _transportGate = new(1, 1);
    private TcpClient? _tcpClient;
    private IModbusMaster? _master;
    private ushort _lastHeartbeat;

    public LogoModbusClient(MachineConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsConnected => _tcpClient?.Connected == true && _master is not null;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _transportGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DisconnectCore();

            var tcpClient = new TcpClient
            {
                ReceiveTimeout = 1500,
                SendTimeout = 1500,
                NoDelay = true
            };

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ConnectTimeoutMs);

            try
            {
                await tcpClient.ConnectAsync(_configuration.IpAddress, _configuration.Port, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                tcpClient.Dispose();
                throw new TimeoutException(
                    $"TCP-Verbindung zu {_configuration.Name} ({_configuration.IpAddress}:{_configuration.Port}) " +
                    $"konnte innerhalb von {ConnectTimeoutMs} ms nicht hergestellt werden.");
            }
            catch (Exception ex)
            {
                tcpClient.Dispose();
                throw new InvalidOperationException(
                    $"TCP-Verbindung zu {_configuration.Name} ({_configuration.IpAddress}:{_configuration.Port}) fehlgeschlagen: {ex.Message}",
                    ex);
            }

            _tcpClient = tcpClient;
            _master = new ModbusFactory().CreateMaster(tcpClient);

            // NModbus retries timed-out requests with a new transaction ID. A delayed
            // response from the preceding attempt must be consumed and ignored rather
            // than poisoning the following request with an ID-mismatch exception.
            _master.Transport.RetryOnOldResponseThreshold = StaleResponseTransactionWindow;
        }
        finally
        {
            _transportGate.Release();
        }
    }

    public async Task WriteJobAsync(
        JobParameters job,
        ushort commandSequence,
        bool automaticMode = true,
        bool resetJob = true,
        bool pauseCounting = false,
        CancellationToken cancellationToken = default)
    {
        await _transportGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            cancellationToken.ThrowIfCancellationRequested();

            // Protocol V3 requires HR12/PcHeartbeat in the range 1..32767.
            // A full job write must therefore never overwrite a valid heartbeat with zero.
            var heartbeat = _lastHeartbeat == 0 ? (ushort)1 : _lastHeartbeat;
            _lastHeartbeat = heartbeat;

            var registers = BuildJobRegisterPayload(
                job,
                commandSequence,
                heartbeat,
                automaticMode,
                resetJob,
                pauseCounting);

            await _master!.WriteMultipleRegistersAsync(
                _configuration.UnitId,
                ModbusRegisterMap.ConfigStart,
                registers).ConfigureAwait(false);
        }
        finally
        {
            _transportGate.Release();
        }
    }

    public static ushort[] BuildJobRegisterPayload(
        JobParameters job,
        ushort commandSequence,
        ushort heartbeat,
        bool automaticMode = true,
        bool resetJob = true,
        bool pauseCounting = false)
    {
        ValidateJob(job);

        if (commandSequence is 0 or > ModbusRegisterMap.MaxSequenceValue)
            throw new ArgumentOutOfRangeException(nameof(commandSequence), $"Command sequence must be 1..{ModbusRegisterMap.MaxSequenceValue}.");
        if (heartbeat is 0 or > ModbusRegisterMap.MaxHeartbeatValue)
            throw new ArgumentOutOfRangeException(nameof(heartbeat), $"PC heartbeat must be 1..{ModbusRegisterMap.MaxHeartbeatValue}.");

        var commandWord = automaticMode ? ModbusRegisterMap.CommandEnableAutomatic : (ushort)0;
        if (resetJob)
            commandWord |= ModbusRegisterMap.CommandResetJob;
        if (pauseCounting)
            commandWord |= ModbusRegisterMap.CommandPauseCounting;

        return
        [
            ModbusRegisterMap.ProtocolVersion,
            commandSequence,
            commandWord,
            job.ActiveCavities,
            ModbusRegisterMap.HighWord(job.TargetPartsPerVe),
            ModbusRegisterMap.LowWord(job.TargetPartsPerVe),
            ModbusRegisterMap.ToValvePulse10Ms(job.ValvePulseMs),
            ModbusRegisterMap.HighWord(job.JobId),
            ModbusRegisterMap.LowWord(job.JobId),
            ModbusRegisterMap.HighWord(job.TargetCyclesPerVe),
            ModbusRegisterMap.LowWord(job.TargetCyclesPerVe),
            heartbeat,
            job.HoldAfterVeNumber
        ];
    }

    public async Task SendCommandAsync(
        ushort commandSequence,
        ushort commandWord,
        CancellationToken cancellationToken = default)
    {
        await _transportGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            cancellationToken.ThrowIfCancellationRequested();

            if (commandSequence is 0 or > ModbusRegisterMap.MaxSequenceValue)
                throw new ArgumentOutOfRangeException(nameof(commandSequence));

            await _master!.WriteMultipleRegistersAsync(
                _configuration.UnitId,
                (ushort)(ModbusRegisterMap.ConfigStart + ModbusRegisterMap.ConfigCommandSequence),
                [commandSequence, commandWord]).ConfigureAwait(false);
        }
        finally
        {
            _transportGate.Release();
        }
    }

    public async Task WriteHeartbeatAsync(ushort heartbeat, CancellationToken cancellationToken = default)
    {
        await _transportGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            cancellationToken.ThrowIfCancellationRequested();

            if (heartbeat is 0 or > ModbusRegisterMap.MaxHeartbeatValue)
                throw new ArgumentOutOfRangeException(nameof(heartbeat));

            await _master!.WriteSingleRegisterAsync(
                _configuration.UnitId,
                (ushort)(ModbusRegisterMap.ConfigStart + ModbusRegisterMap.ConfigPcHeartbeat),
                heartbeat).ConfigureAwait(false);
            _lastHeartbeat = heartbeat;
        }
        finally
        {
            _transportGate.Release();
        }
    }

    public async Task<LogoSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await _transportGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            cancellationToken.ThrowIfCancellationRequested();

            var registers = await _master!.ReadHoldingRegistersAsync(
                _configuration.UnitId,
                ModbusRegisterMap.StatusStart,
                ModbusRegisterMap.StatusLength).ConfigureAwait(false);

            var reportedProtocol = registers[ModbusRegisterMap.StatusProtocolVersion];
            if (reportedProtocol != ModbusRegisterMap.ProtocolVersion)
                throw new InvalidOperationException(
                    $"LOGO! register protocol mismatch at {_configuration.Name} ({_configuration.IpAddress}:{_configuration.Port}): " +
                    $"expected V{ModbusRegisterMap.ProtocolVersion}, received V{reportedProtocol} from HR20/VW38.");

            var activeCavities = registers[ModbusRegisterMap.StatusActiveCavitiesEcho];
            var lastCompletedCavities = registers[ModbusRegisterMap.StatusLastCompletedCavities];
            var currentVeCycles = ModbusRegisterMap.ToUInt32(
                registers[ModbusRegisterMap.StatusCurrentVeCyclesHi],
                registers[ModbusRegisterMap.StatusCurrentVeCyclesLo]);
            var lastCompletedVeCycles = ModbusRegisterMap.ToUInt32(
                registers[ModbusRegisterMap.StatusLastVeCyclesHi],
                registers[ModbusRegisterMap.StatusLastVeCyclesLo]);

            if (currentVeCycles > ModbusRegisterMap.MaxTargetCyclesPerVe || lastCompletedVeCycles > ModbusRegisterMap.MaxTargetCyclesPerVe)
                throw new InvalidOperationException("LOGO! reported a VE cycle counter outside the Partcounter V3 range.");

            var currentParts = checked(currentVeCycles * (uint)activeCavities);
            var lastCompletedVeQuantity = checked(lastCompletedVeCycles * (uint)lastCompletedCavities);
            var totalCycles = ModbusRegisterMap.ToUInt32(
                registers[ModbusRegisterMap.StatusTotalCyclesHi],
                registers[ModbusRegisterMap.StatusTotalCyclesLo]);
            if (totalCycles > ModbusRegisterMap.MaxTotalCyclesPerJob)
                throw new InvalidOperationException("LOGO! reported a total-cycle counter outside the approved Partcounter V3 range.");

            return new LogoSnapshot(
                currentParts,
                totalCycles,
                registers[ModbusRegisterMap.StatusCurrentVe],
                registers[ModbusRegisterMap.StatusCompletedVes],
                lastCompletedVeQuantity,
                registers[ModbusRegisterMap.StatusWord],
                registers[ModbusRegisterMap.StatusAckSequence],
                activeCavities,
                registers[ModbusRegisterMap.StatusLastCompletedVeNumber],
                registers[ModbusRegisterMap.StatusCompletionSequence],
                registers[ModbusRegisterMap.StatusLogoHeartbeat],
                registers[ModbusRegisterMap.StatusErrorCode],
                (VeCompletionReason)registers[ModbusRegisterMap.StatusLastCompletionReason],
                DateTime.UtcNow,
                registers[ModbusRegisterMap.StatusHoldAfterVeNumberEcho],
                ModbusRegisterMap.ToUInt32(
                    registers[ModbusRegisterMap.StatusJobIdHiEcho],
                    registers[ModbusRegisterMap.StatusJobIdLoEcho]));
        }
        finally
        {
            _transportGate.Release();
        }
    }

    public void Disconnect()
    {
        _transportGate.Wait();
        try
        {
            DisconnectCore();
        }
        finally
        {
            _transportGate.Release();
        }
    }

    private void DisconnectCore()
    {
        _master?.Dispose();
        _master = null;
        _tcpClient?.Dispose();
        _tcpClient = null;
    }

    private static void ValidateJob(JobParameters job)
    {
        if (job.ActiveCavities is < 1 or > 64)
            throw new ArgumentOutOfRangeException(nameof(job), "Active cavities must be between 1 and 64.");

        if (!JobInstanceIdFactory.IsLogoWordSafe(job.JobId))
            throw new ArgumentOutOfRangeException(nameof(job), "JobId must be nonzero and each 16-bit word must stay in the LOGO analog-safe range 0..32767.");

        if (job.TargetPartsPerVe == 0)
            throw new ArgumentOutOfRangeException(nameof(job), "Target parts per VE must be greater than zero.");

        if (job.TargetCyclesPerVe is 0 or > ModbusRegisterMap.MaxTargetCyclesPerVe)
            throw new ArgumentOutOfRangeException(nameof(job), $"Target cycles per VE must be between 1 and {ModbusRegisterMap.MaxTargetCyclesPerVe:N0}.");

        if (job.ValvePulseMs < ModbusRegisterMap.MinValvePulseMs || job.ValvePulseMs > ModbusRegisterMap.MaxValvePulseMs)
            throw new ArgumentOutOfRangeException(nameof(job), $"Valve pulse must be between {ModbusRegisterMap.MinValvePulseMs} and {ModbusRegisterMap.MaxValvePulseMs} ms.");

        if (job.ValvePulseMs % ModbusRegisterMap.ValvePulseUnitMs != 0)
            throw new ArgumentOutOfRangeException(nameof(job), $"Valve pulse must be a multiple of {ModbusRegisterMap.ValvePulseUnitMs} ms.");

        if (job.HoldAfterVeNumber is 0 or > ModbusRegisterMap.MaxVeNumber)
            throw new ArgumentOutOfRangeException(nameof(job), $"Hold-after VE must be 1..{ModbusRegisterMap.MaxVeNumber:N0} for Protocol V3 production commands.");
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new InvalidOperationException($"Machine {_configuration.Name} is not connected.");
    }

    public async ValueTask DisposeAsync()
    {
        await _transportGate.WaitAsync().ConfigureAwait(false);
        try
        {
            DisconnectCore();
        }
        finally
        {
            _transportGate.Release();
        }
    }
}
