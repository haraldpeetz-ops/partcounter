using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Partcounter.Models;
using Partcounter.Services;
using Xunit;

namespace Partcounter.Tests;

public sealed class LogoModbusIntegrationTests
{
    [Fact]
    public async Task SharedClient_SerializesConcurrentModbusTransactions()
    {
        await using var server = new FakeLogoModbusServer();
        var configuration = new MachineConfiguration(
            1,
            "M01-Concurrency",
            IPAddress.Loopback.ToString(),
            server.Port,
            1,
            true);

        await using var client = new LogoModbusClient(configuration);
        await client.ConnectAsync();

        var operations = Enumerable.Range(1, 64)
            .Select(index => index % 2 == 0
                ? client.WriteHeartbeatAsync(checked((ushort)index))
                : client.ReadSnapshotAsync())
            .ToArray();

        await Task.WhenAll(operations);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task DelayedResponseFromPriorRetry_IsDiscardedWithoutPoisoningTransport()
    {
        await using var server = new FakeLogoModbusServer(delayFirstResponseMs: 1_750);
        var configuration = new MachineConfiguration(
            1,
            "M01-DelayedResponse",
            IPAddress.Loopback.ToString(),
            server.Port,
            1,
            true);

        await using var client = new LogoModbusClient(configuration);
        await client.ConnectAsync();

        var snapshot = await client.ReadSnapshotAsync();
        await client.WriteHeartbeatAsync(31);

        Assert.True((snapshot.StatusWord & ModbusRegisterMap.StatusReady) != 0);
        Assert.Equal((ushort)31, server.GetRegister(ModbusRegisterMap.ConfigPcHeartbeat));
        Assert.True(server.GetRequestCount(3) >= 2);
    }

    [Fact]
    public async Task ProtocolV3_Loopback_EndToEnd_ConnectWriteReadAndReconnect()
    {
        await using var server = new FakeLogoModbusServer();
        var configuration = new MachineConfiguration(
            1,
            "M01-Test",
            IPAddress.Loopback.ToString(),
            server.Port,
            1,
            true);

        await using var client = new LogoModbusClient(configuration);
        await client.ConnectAsync();

        await client.WriteHeartbeatAsync(17);
        var job = new JobParameters(
            0x00010001,
            "A-1",
            "WZ-1",
            8,
            1000,
            125,
            750,
            7);

        await client.WriteJobAsync(job, 23);
        var snapshot = await client.ReadSnapshotAsync();

        await client.SendCommandAsync(24, ModbusRegisterMap.CommandEnableAutomatic);
        var afterCommand = await client.ReadSnapshotAsync();

        Assert.Equal((ushort)3, server.GetRegister(ModbusRegisterMap.StatusStart));
        Assert.Equal((ushort)17, server.GetRegister(ModbusRegisterMap.ConfigPcHeartbeat));
        Assert.Equal((ushort)7, server.GetRegister(ModbusRegisterMap.ConfigHoldAfterVeNumber));
        Assert.Equal((ushort)23, snapshot.AcknowledgedCommandSequence);
        Assert.Equal((ushort)8, snapshot.ActiveCavitiesEcho);
        Assert.Equal((ushort)7, snapshot.HoldAfterVeNumberEcho);
        Assert.Equal(job.JobId, snapshot.JobIdEcho);
        Assert.True((snapshot.StatusWord & ModbusRegisterMap.StatusCompletionHoldArmed) != 0);
        Assert.Equal((ushort)24, afterCommand.AcknowledgedCommandSequence);
        Assert.Equal(ModbusRegisterMap.CommandEnableAutomatic, server.GetRegister(ModbusRegisterMap.ConfigCommandWord));
        Assert.True(server.GetRequestCount(3) >= 2);
        Assert.True(server.GetRequestCount(6) >= 1);
        Assert.True(server.GetRequestCount(16) >= 2);

        client.Disconnect();
        await client.ConnectAsync();
        var afterReconnect = await client.ReadSnapshotAsync();

        Assert.Equal((ushort)24, afterReconnect.AcknowledgedCommandSequence);
        Assert.Equal(job.JobId, afterReconnect.JobIdEcho);
        Assert.Equal((ushort)7, afterReconnect.HoldAfterVeNumberEcho);
    }

    [Fact]
    public async Task ProtocolMismatch_IsRejectedWithEndpointAndRegisterDiagnostic()
    {
        await using var server = new FakeLogoModbusServer();
        server.SetRegister(
            ModbusRegisterMap.StatusStart + ModbusRegisterMap.StatusProtocolVersion,
            2);
        var configuration = new MachineConfiguration(
            1,
            "M01-Mismatch",
            IPAddress.Loopback.ToString(),
            server.Port,
            1,
            true);

        await using var client = new LogoModbusClient(configuration);
        await client.ConnectAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.ReadSnapshotAsync());
        Assert.Contains("expected V3", error.Message);
        Assert.Contains("received V2", error.Message);
        Assert.Contains("HR20/VW38", error.Message);
        Assert.Contains(server.Port.ToString(), error.Message);
    }

    private sealed class FakeLogoModbusServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _runTask;
        private readonly ushort[] _registers = new ushort[128];
        private readonly int[] _functionCounts = new int[256];
        private readonly object _sync = new();
        private readonly int _delayFirstResponseMs;
        private int _responseSequence;

        public FakeLogoModbusServer(int delayFirstResponseMs = 0)
        {
            _delayFirstResponseMs = delayFirstResponseMs;
            _registers[ModbusRegisterMap.StatusStart + ModbusRegisterMap.StatusProtocolVersion] = ModbusRegisterMap.ProtocolVersion;
            _registers[ModbusRegisterMap.StatusStart + ModbusRegisterMap.StatusWord] =
                (ushort)(ModbusRegisterMap.StatusReady | ModbusRegisterMap.StatusCompletionHoldArmed);

            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _runTask = RunAsync(_cts.Token);
        }

        public int Port { get; }

        public ushort GetRegister(int zeroBasedAddress)
        {
            lock (_sync)
                return _registers[zeroBasedAddress];
        }

        public void SetRegister(int zeroBasedAddress, ushort value)
        {
            lock (_sync)
                _registers[zeroBasedAddress] = value;
        }

        public int GetRequestCount(byte functionCode) => Volatile.Read(ref _functionCounts[functionCode]);

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (SocketException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                using (client)
                using (var stream = client.GetStream())
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            if (!await ProcessRequestAsync(stream, cancellationToken))
                                break;
                        }
                        catch (EndOfStreamException)
                        {
                            break;
                        }
                        catch (IOException)
                        {
                            break;
                        }
                    }
                }
            }
        }

        private async Task<bool> ProcessRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var header = new byte[7];
            try
            {
                await stream.ReadExactlyAsync(header, cancellationToken);
            }
            catch (EndOfStreamException)
            {
                return false;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));
            if (length < 2)
                throw new InvalidDataException("Invalid Modbus/TCP length.");

            var pdu = new byte[length - 1];
            await stream.ReadExactlyAsync(pdu, cancellationToken);
            var responsePdu = BuildResponse(pdu);

            if (Interlocked.Increment(ref _responseSequence) == 1 && _delayFirstResponseMs > 0)
                await Task.Delay(_delayFirstResponseMs, cancellationToken);

            var responseHeader = new byte[7];
            header.AsSpan(0, 4).CopyTo(responseHeader);
            BinaryPrimitives.WriteUInt16BigEndian(responseHeader.AsSpan(4, 2), checked((ushort)(responsePdu.Length + 1)));
            responseHeader[6] = header[6];

            await stream.WriteAsync(responseHeader, cancellationToken);
            await stream.WriteAsync(responsePdu, cancellationToken);
            return true;
        }

        private byte[] BuildResponse(byte[] pdu)
        {
            if (pdu.Length == 0)
                throw new InvalidDataException("Empty Modbus PDU.");

            Interlocked.Increment(ref _functionCounts[pdu[0]]);

            return pdu[0] switch
            {
                3 => HandleReadHoldingRegisters(pdu),
                6 => HandleWriteSingleRegister(pdu),
                16 => HandleWriteMultipleRegisters(pdu),
                _ => throw new InvalidDataException($"Unsupported Modbus function {pdu[0]}.")
            };
        }

        private byte[] HandleReadHoldingRegisters(byte[] pdu)
        {
            if (pdu.Length != 5)
                throw new InvalidDataException("Invalid FC03 request.");

            var start = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(1, 2));
            var count = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(3, 2));
            var response = new byte[2 + count * 2];
            response[0] = 3;
            response[1] = checked((byte)(count * 2));

            lock (_sync)
            {
                for (var i = 0; i < count; i++)
                    BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(2 + i * 2, 2), _registers[start + i]);
            }

            return response;
        }

        private byte[] HandleWriteSingleRegister(byte[] pdu)
        {
            if (pdu.Length != 5)
                throw new InvalidDataException("Invalid FC06 request.");

            var address = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(1, 2));
            var value = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(3, 2));
            lock (_sync)
                _registers[address] = value;

            return pdu.ToArray();
        }

        private byte[] HandleWriteMultipleRegisters(byte[] pdu)
        {
            if (pdu.Length < 6)
                throw new InvalidDataException("Invalid FC16 request.");

            var start = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(1, 2));
            var count = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(3, 2));
            var byteCount = pdu[5];
            if (byteCount != count * 2 || pdu.Length != 6 + byteCount)
                throw new InvalidDataException("Invalid FC16 register payload.");

            lock (_sync)
            {
                for (var i = 0; i < count; i++)
                    _registers[start + i] = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(6 + i * 2, 2));

                if (start == ModbusRegisterMap.ConfigStart && count == ModbusRegisterMap.ConfigLength)
                {
                    _registers[ModbusRegisterMap.StatusStart + ModbusRegisterMap.StatusProtocolVersion] = ModbusRegisterMap.ProtocolVersion;
                    _registers[ModbusRegisterMap.StatusStart + ModbusRegisterMap.StatusWord] =
                        (ushort)(ModbusRegisterMap.StatusReady | ModbusRegisterMap.StatusCompletionHoldArmed);
                    _registers[ModbusRegisterMap.StatusStart + ModbusRegisterMap.StatusAckSequence] =
                        _registers[ModbusRegisterMap.ConfigCommandSequence];
                    _registers[ModbusRegisterMap.StatusStart + ModbusRegisterMap.StatusActiveCavitiesEcho] =
                        _registers[ModbusRegisterMap.ConfigActiveCavities];
                    _registers[ModbusRegisterMap.StatusStart + ModbusRegisterMap.StatusHoldAfterVeNumberEcho] =
                        _registers[ModbusRegisterMap.ConfigHoldAfterVeNumber];
                    _registers[ModbusRegisterMap.StatusStart + ModbusRegisterMap.StatusJobIdHiEcho] =
                        _registers[ModbusRegisterMap.ConfigJobIdHi];
                    _registers[ModbusRegisterMap.StatusStart + ModbusRegisterMap.StatusJobIdLoEcho] =
                        _registers[ModbusRegisterMap.ConfigJobIdLo];
                }
                var commandSequenceAddress = ModbusRegisterMap.ConfigStart + ModbusRegisterMap.ConfigCommandSequence;
                if (start <= commandSequenceAddress && start + count > commandSequenceAddress)
                {
                    _registers[ModbusRegisterMap.StatusStart + ModbusRegisterMap.StatusAckSequence] =
                        _registers[commandSequenceAddress];
                }
            }

            var response = new byte[5];
            response[0] = 16;
            BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(1, 2), start);
            BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(3, 2), count);
            return response;
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _listener.Stop();
            try
            {
                await _runTask;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _cts.Dispose();
            }
        }
    }
}
