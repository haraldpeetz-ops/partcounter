using System.Security.Cryptography;

namespace Partcounter.Services;

public static class JobInstanceIdFactory
{
    public static uint Create()
    {
        var high = (ushort)RandomNumberGenerator.GetInt32(0, ModbusRegisterMap.MaxSequenceValue + 1);
        var low = (ushort)RandomNumberGenerator.GetInt32(1, ModbusRegisterMap.MaxSequenceValue + 1);
        return ((uint)high << 16) | low;
    }

    public static bool IsLogoWordSafe(uint value) =>
        value != 0 &&
        ModbusRegisterMap.HighWord(value) <= ModbusRegisterMap.MaxSequenceValue &&
        ModbusRegisterMap.LowWord(value) <= ModbusRegisterMap.MaxSequenceValue;
}
