namespace Partcounter.Services;

public static class ModbusRegisterMap
{
    // NModbus addresses are zero based. Address 0 corresponds to LOGO! Holding Register 1 / VW0.
    public const ushort ConfigStart = 0;
    public const ushort ConfigLength = 13;

    public const int ConfigProtocolVersion = 0;
    public const int ConfigCommandSequence = 1;
    public const int ConfigCommandWord = 2;
    public const int ConfigActiveCavities = 3;
    public const int ConfigTargetPartsHi = 4;
    public const int ConfigTargetPartsLo = 5;
    public const int ConfigValvePulse10Ms = 6;
    public const int ConfigJobIdHi = 7;
    public const int ConfigJobIdLo = 8;
    public const int ConfigTargetCyclesHi = 9;
    public const int ConfigTargetCyclesLo = 10;
    public const int ConfigPcHeartbeat = 11;
    public const int ConfigHoldAfterVeNumber = 12;

    public const ushort StatusStart = 19; // HR20 / VW38
    public const ushort StatusLength = 21;

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
    public const int StatusHoldAfterVeNumberEcho = 18;
    public const int StatusJobIdHiEcho = 19;
    public const int StatusJobIdLoEcho = 20;

    public const ushort ProtocolVersion = 3;

    // LOGO! analog references and arithmetic use signed 16-bit integer values.
    // Keep all values that must be copied/compared inside the LOGO! in the positive 16-bit range.
    public const uint MaxTargetCyclesPerVe = 32_767;
    public const uint MaxTotalCyclesPerJob = 999_999;
    public const ushort MaxSequenceValue = 32_767;
    public const ushort MaxHeartbeatValue = 32_767;
    public const ushort MaxVeNumber = 32_767;

    // The LOGO! timer parameter uses a fixed 10 ms time base in Partcounter_LOGO_V001.
    public const ushort ValvePulseUnitMs = 10;
    public const ushort MinValvePulseMs = 50;
    public const ushort MaxValvePulseMs = 5_000;

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
    public const ushort StatusCompletionHoldArmed = 1 << 6;
    public const ushort StatusCompletionHoldActive = 1 << 7;

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

    public static ushort ToValvePulse10Ms(ushort milliseconds)
    {
        if (milliseconds % ValvePulseUnitMs != 0)
            throw new ArgumentOutOfRangeException(nameof(milliseconds), $"Valve pulse must be a multiple of {ValvePulseUnitMs} ms.");

        return (ushort)(milliseconds / ValvePulseUnitMs);
    }
}
