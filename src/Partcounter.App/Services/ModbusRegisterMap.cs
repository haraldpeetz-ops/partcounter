namespace Partcounter.Services;

public static class ModbusRegisterMap
{
    // NModbus addresses are zero based. Address 0 corresponds to LOGO! Holding Register 1 / VW0.
    public const ushort ConfigStart = 0;
    public const ushort ConfigLength = 10;

    public const int ConfigProtocolVersion = 0;
    public const int ConfigCommandSequence = 1;
    public const int ConfigCommandWord = 2;
    public const int ConfigActiveCavities = 3;
    public const int ConfigTargetPartsHi = 4;
    public const int ConfigTargetPartsLo = 5;
    public const int ConfigValvePulseMs = 6;
    public const int ConfigJobIdHi = 7;
    public const int ConfigJobIdLo = 8;
    public const int ConfigReserved = 9;

    public const ushort StatusStart = 19; // HR20 / VW38
    public const ushort StatusLength = 12;

    public const int StatusProtocolVersion = 0;
    public const int StatusWord = 1;
    public const int StatusCurrentPartsHi = 2;
    public const int StatusCurrentPartsLo = 3;
    public const int StatusTotalCyclesHi = 4;
    public const int StatusTotalCyclesLo = 5;
    public const int StatusCurrentVe = 6;
    public const int StatusCompletedVes = 7;
    public const int StatusLastVeQuantityHi = 8;
    public const int StatusLastVeQuantityLo = 9;
    public const int StatusAckSequence = 10;
    public const int StatusActiveCavitiesEcho = 11;

    public const ushort ProtocolVersion = 1;

    public const ushort CommandEnableAutomatic = 1 << 0;
    public const ushort CommandResetJob = 1 << 1;
    public const ushort CommandManualVeChange = 1 << 2;
    public const ushort CommandAcknowledgeAlarm = 1 << 3;

    public static ushort HighWord(uint value) => (ushort)(value >> 16);
    public static ushort LowWord(uint value) => (ushort)(value & 0xFFFF);
    public static uint ToUInt32(ushort high, ushort low) => ((uint)high << 16) | low;
}
