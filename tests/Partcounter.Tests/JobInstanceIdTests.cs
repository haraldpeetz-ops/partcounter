using Partcounter.Services;
using Xunit;

namespace Partcounter.Tests;

public sealed class JobInstanceIdTests
{
    [Fact]
    public void GeneratedIds_AreNonZeroAndLogoWordSafe()
    {
        for (var i = 0; i < 256; i++)
        {
            var value = JobInstanceIdFactory.Create();
            Assert.NotEqual(0u, value);
            Assert.True(JobInstanceIdFactory.IsLogoWordSafe(value));
            Assert.InRange(ModbusRegisterMap.HighWord(value), (ushort)0, ModbusRegisterMap.MaxSequenceValue);
            Assert.InRange(ModbusRegisterMap.LowWord(value), (ushort)1, ModbusRegisterMap.MaxSequenceValue);
        }
    }

    [Theory]
    [InlineData(0u, false)]
    [InlineData(1u, true)]
    [InlineData(2147450879u, true)] // 0x7FFF7FFF
    [InlineData(2147483648u, false)] // high word 0x8000
    [InlineData(32768u, false)]      // low word 0x8000
    public void WordSafety_IsExplicit(uint value, bool expected)
    {
        Assert.Equal(expected, JobInstanceIdFactory.IsLogoWordSafe(value));
    }
}
