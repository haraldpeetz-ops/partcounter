namespace Partcounter.Models;

public sealed record LiveCommissioningSample(
    DateTime TimestampUtc,
    bool DiagnosticsAvailable,
    ConnectionState ConnectionState,
    ushort PcHeartbeat,
    ushort LocalCommandSequence,
    bool CommandSequenceSynchronized,
    ushort AckSequence,
    ushort LogoHeartbeat,
    ushort StatusWord,
    ushort ErrorCode,
    ushort CompletionSequence,
    ushort ActiveCavities,
    uint CurrentParts,
    uint TotalCycles,
    ushort CurrentVeNumber,
    ushort CompletedVes,
    DateTime? SourceSnapshotUtc,
    string StatusText,
    string? Message)
{
    public string TimestampText => TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff");
    public string ConnectionText => DiagnosticsAvailable
        ? ConnectionState.ToString().ToUpperInvariant()
        : $"{ConnectionState.ToString().ToUpperInvariant()} / keine Diagnose";
    public string SequenceText => CommandSequenceSynchronized
        ? $"{LocalCommandSequence} / {AckSequence}"
        : $"{LocalCommandSequence} / {AckSequence} !";
    public string CounterText => $"{TotalCycles:N0} Zyklen / {CurrentParts:N0} Teile";
    public string VeText => $"VE {CurrentVeNumber} · fertig {CompletedVes} · Seq {CompletionSequence}";
    public string ErrorText => ErrorCode == 0 ? "0" : ErrorCode.ToString();
}

public sealed record LiveCommissioningSummary(
    int SampleCount,
    TimeSpan Duration,
    int OnlineSamples,
    int OfflineSamples,
    int FaultSamples,
    int ConnectionDropCount,
    int RecoveryCount,
    bool PcHeartbeatChanged,
    bool LogoHeartbeatChanged,
    int SequenceSyncFailureSamples,
    long TotalCycleDelta,
    long CompletionSequenceDelta,
    long CompletedVeDelta,
    int AlarmSamples,
    int PcHeartbeatStaleSamples,
    int CycleInputActiveSamples,
    ushort FirstPcHeartbeat,
    ushort LastPcHeartbeat,
    ushort FirstLogoHeartbeat,
    ushort LastLogoHeartbeat)
{
    public string DisplayText =>
        $"{SampleCount:N0} Messpunkte · {Duration:hh\\:mm\\:ss} · Online {OnlineSamples:N0} / Offline {OfflineSamples:N0} / Fehler {FaultSamples:N0} · " +
        $"Verbindungsabbrüche {ConnectionDropCount:N0}, Wiederkehr {RecoveryCount:N0} · Zyklen Δ {TotalCycleDelta:+#;-#;0} · " +
        $"CompletionSequence Δ {CompletionSequenceDelta:+#;-#;0} · VE fertig Δ {CompletedVeDelta:+#;-#;0} · " +
        $"HB PC {(PcHeartbeatChanged ? "ändert sich" : "ohne Änderung")}, LOGO {(LogoHeartbeatChanged ? "ändert sich" : "ohne Änderung")} · " +
        $"Seq.-Unsync {SequenceSyncFailureSamples:N0} · Alarm {AlarmSamples:N0} · PC-HB stale {PcHeartbeatStaleSamples:N0}.";
}
