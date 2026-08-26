namespace Partcounter.Services;

public static class ModbusRegisterMap
{
    // NModbus addresses are zero based. Address 0 corresponds to LOGO! Holding Register 1 / VW0.
    public const ushort ConfigStart = 0;
    public const ushort ConfigLength = 12;

    public const int ConfigProtocolVersion = 0;
    public const int ConfigCommandSequence = 1;
    public const int ConfigCommandWord = 2;
    public const int ConfigActiveCavities = 3;
    public const int ConfigTargetPartsHi = 4;
    public const int ConfigTargetPartsLo = 5;
    public const int ConfigValvePulseMs = 6;
    public const int ConfigJobIdHi = 7;
    public const int ConfigJobIdLo = 8;
    public const int ConfigTargetCyclesHi = 9;
    public const int ConfigTargetCyclesLo = 10;
    public const int ConfigPcHeartbeat = 11;

    public const ushort StatusStart = 19; // HR20 / VW38
    public const ushort StatusLength = 18;

    public const int StatusProtocolVersion = 0;
    public const int StatusWord = 1;
    public const int StatusCurrentVeCyclesHi = 2;
    public const int StatusCurrentVeCyclesLo = 3;
    public const int StatusTotalCyclesHi = 4;
    public const int StatusTotalCyclesLo = 5;
    public const int StatusCurrentVe = 6;
    public const int StatusCompletedVes = 7;
    public const int StatusLastVeCyclesHi = 8;
    public const int StatusLastVeCyclesLo = 9;
    public const int StatusAckSequence = 10;
    public const int StatusActiveCavitiesEcho = 11;
    public const int StatusLastCompletedVeNumber = 12;
    public const int StatusCompletionSequence = 13;
    public const int StatusLogoHeartbeat = 14;
    public const int StatusErrorCode = 15;
    public const int StatusLastCompletionReason = 16;
    public const int StatusLastCompletedCavities = 17;

    public const ushort ProtocolVersion = 2;

    public const ushort CommandEnableAutomatic = 1 << 0;
    public const ushort CommandResetJob = 1 << 1;
    public const ushort CommandManualVeChange = 1 << 2;
    public const ushort CommandAcknowledgeAlarm = 1 << 3;
    public const ushort CommandPauseCounting = 1 << 4;

    public const ushort StatusReady = 1 << 0;
    public const ushort StatusAutomaticEnabled = 1 << 1;
    public const ushort StatusVeChangeActive = 1 << 2;
    public const ushort StatusAlarm = 1 << 3;
    public const ushort StatusCycleInputActive = 1 << 4;
    public const ushort StatusPcHeartbeatStale = 1 << 5;

    public const ushort ErrorNone = 0;
    public const ushort ErrorProtocolVersion = 1;
    public const ushort ErrorInvalidCavities = 2;
    public const ushort ErrorInvalidTargetParts = 3;
    public const ushort ErrorInvalidTargetCycles = 4;
    public const ushort ErrorInvalidValvePulse = 5;
    public const ushort ErrorVeChangerTimeout = 10;
    public const ushort ErrorInternalState = 30;

    public static ushort HighWord(uint value) => (ushort)(value >> 16);
    public static ushort LowWord(uint value) => (ushort)(value & 0xFFFF);
    public static uint ToUInt32(ushort high, ushort low) => ((uint)high << 16) | low;
}
