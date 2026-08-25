using System.Net.Sockets;
using NModbus;
using Partcounter.Models;

namespace Partcounter.Services;

public sealed class LogoModbusClient : IAsyncDisposable
{
    private readonly MachineConfiguration _configuration;
    private TcpClient? _tcpClient;
    private IModbusMaster? _master;

    public LogoModbusClient(MachineConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsConnected => _tcpClient?.Connected == true && _master is not null;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        Disconnect();

        var tcpClient = new TcpClient
        {
            ReceiveTimeout = 1500,
            SendTimeout = 1500,
            NoDelay = true
        };

        await tcpClient.ConnectAsync(_configuration.IpAddress, _configuration.Port, cancellationToken);
        _tcpClient = tcpClient;
        _master = new ModbusFactory().CreateMaster(tcpClient);
    }

    public async Task WriteJobAsync(
        JobParameters job,
        ushort commandSequence,
        bool automaticMode = true,
        bool resetJob = true,
        bool pauseCounting = false,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        cancellationToken.ThrowIfCancellationRequested();

        var commandWord = automaticMode ? ModbusRegisterMap.CommandEnableAutomatic : (ushort)0;
        if (resetJob)
            commandWord |= ModbusRegisterMap.CommandResetJob;
        if (pauseCounting)
            commandWord |= ModbusRegisterMap.CommandPauseCounting;

        ushort[] registers =
        [
            ModbusRegisterMap.ProtocolVersion,
            commandSequence,
            commandWord,
            job.ActiveCavities,
            ModbusRegisterMap.HighWord(job.TargetPartsPerVe),
            ModbusRegisterMap.LowWord(job.TargetPartsPerVe),
            job.ValvePulseMs,
            ModbusRegisterMap.HighWord(job.JobId),
            ModbusRegisterMap.LowWord(job.JobId),
            ModbusRegisterMap.HighWord(job.TargetCyclesPerVe),
            ModbusRegisterMap.LowWord(job.TargetCyclesPerVe),
            0
        ];

        await _master!.WriteMultipleRegistersAsync(
            _configuration.UnitId,
            ModbusRegisterMap.ConfigStart,
            registers);
    }

    public async Task SendCommandAsync(
        ushort commandSequence,
        ushort commandWord,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        cancellationToken.ThrowIfCancellationRequested();

        await _master!.WriteMultipleRegistersAsync(
            _configuration.UnitId,
            (ushort)(ModbusRegisterMap.ConfigStart + ModbusRegisterMap.ConfigCommandSequence),
            [commandSequence, commandWord]);
    }

    public async Task WriteHeartbeatAsync(ushort heartbeat, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        cancellationToken.ThrowIfCancellationRequested();

        await _master!.WriteSingleRegisterAsync(
            _configuration.UnitId,
            (ushort)(ModbusRegisterMap.ConfigStart + ModbusRegisterMap.ConfigPcHeartbeat),
            heartbeat);
    }

    public async Task<LogoSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        cancellationToken.ThrowIfCancellationRequested();

        var registers = await _master!.ReadHoldingRegistersAsync(
            _configuration.UnitId,
            ModbusRegisterMap.StatusStart,
            ModbusRegisterMap.StatusLength);

        if (registers[ModbusRegisterMap.StatusProtocolVersion] != ModbusRegisterMap.ProtocolVersion)
            throw new InvalidOperationException("LOGO! register protocol version does not match Partcounter.");

        return new LogoSnapshot(
            ModbusRegisterMap.ToUInt32(registers[ModbusRegisterMap.StatusCurrentPartsHi], registers[ModbusRegisterMap.StatusCurrentPartsLo]),
            ModbusRegisterMap.ToUInt32(registers[ModbusRegisterMap.StatusTotalCyclesHi], registers[ModbusRegisterMap.StatusTotalCyclesLo]),
            registers[ModbusRegisterMap.StatusCurrentVe],
            registers[ModbusRegisterMap.StatusCompletedVes],
            ModbusRegisterMap.ToUInt32(registers[ModbusRegisterMap.StatusLastVeQuantityHi], registers[ModbusRegisterMap.StatusLastVeQuantityLo]),
            registers[ModbusRegisterMap.StatusWord],
            registers[ModbusRegisterMap.StatusAckSequence],
            registers[ModbusRegisterMap.StatusActiveCavitiesEcho],
            registers[ModbusRegisterMap.StatusLastCompletedVeNumber],
            registers[ModbusRegisterMap.StatusCompletionSequence],
            registers[ModbusRegisterMap.StatusLogoHeartbeat],
            registers[ModbusRegisterMap.StatusErrorCode],
            (VeCompletionReason)registers[ModbusRegisterMap.StatusLastCompletionReason],
            DateTime.UtcNow);
    }

    public void Disconnect()
    {
        _master?.Dispose();
        _master = null;
        _tcpClient?.Dispose();
        _tcpClient = null;
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new InvalidOperationException($"Machine {_configuration.Name} is not connected.");
    }

    public ValueTask DisposeAsync()
    {
        Disconnect();
        return ValueTask.CompletedTask;
    }
}
