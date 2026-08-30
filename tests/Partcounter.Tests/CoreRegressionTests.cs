using Microsoft.Data.Sqlite;
using Partcounter.Models;
using Partcounter.Services;
using Xunit;

namespace Partcounter.Tests;

public sealed class CoreRegressionTests
{
    [Fact]
    public void SixtyFourCavityRounding_IsDeterministic()
    {
        var article = new ArticleDefinition(1, "A", "Test", "WZ", 64, 1000, true);
        Assert.Equal((uint)16, article.RequiredCycles);
        Assert.Equal((uint)1024, article.EffectivePackagingQuantity);
        Assert.Equal((uint)24, article.ExpectedOverfill);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(65535u)]
    [InlineData(65536u)]
    [InlineData(uint.MaxValue)]
    public void ModbusDword_RoundTrips(uint value)
    {
        Assert.Equal(value, ModbusRegisterMap.ToUInt32(ModbusRegisterMap.HighWord(value), ModbusRegisterMap.LowWord(value)));
    }

    [Theory]
    [InlineData((ushort)50, (ushort)5)]
    [InlineData((ushort)750, (ushort)75)]
    [InlineData((ushort)5000, (ushort)500)]
    public void ValvePulse_UsesTenMillisecondUnits(ushort milliseconds, ushort expected)
    {
        Assert.Equal(expected, ModbusRegisterMap.ToValvePulse10Ms(milliseconds));
    }

    [Fact]
    public async Task SqliteWriteCoordinator_SerializesConcurrentWriters()
    {
        var root = Path.Combine(Path.GetTempPath(), "PartcounterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var db = Path.Combine(root, "test.db");

        await SqliteWriteCoordinator.ExecuteAsync(db, async connection =>
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "CREATE TABLE T(Id INTEGER PRIMARY KEY AUTOINCREMENT, V INTEGER NOT NULL);";
            await cmd.ExecuteNonQueryAsync();
        });

        var tasks = Enumerable.Range(0, 100).Select(i => SqliteWriteCoordinator.ExecuteAsync(db, async connection =>
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO T(V) VALUES($v);";
            cmd.Parameters.AddWithValue("$v", i);
            await cmd.ExecuteNonQueryAsync();
        }));
        await Task.WhenAll(tasks);

        await using var verify = new SqliteConnection(SqliteWriteCoordinator.BuildConnectionString(db));
        await verify.OpenAsync();
        var count = verify.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM T;";
        Assert.Equal(100L, Convert.ToInt64(await count.ExecuteScalarAsync()));
    }

    [Fact]
    public void ProtocolV3_ContractExtendsBuffersWithoutMovingExistingAddresses()
    {
        Assert.Equal((ushort)3, ModbusRegisterMap.ProtocolVersion);
        Assert.Equal((ushort)0, ModbusRegisterMap.ConfigStart);
        Assert.Equal((ushort)13, ModbusRegisterMap.ConfigLength);
        Assert.Equal(12, ModbusRegisterMap.ConfigHoldAfterVeNumber);
        Assert.Equal((ushort)19, ModbusRegisterMap.StatusStart);
        Assert.Equal((ushort)19, ModbusRegisterMap.StatusLength);
        Assert.Equal(18, ModbusRegisterMap.StatusHoldAfterVeNumberEcho);
        Assert.Equal((ushort)32767, ModbusRegisterMap.MaxSequenceValue);
    }
}
