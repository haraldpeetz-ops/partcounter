using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Partcounter.Models;

public enum CommissioningReleaseState
{
    NotTested = 0,
    InTest = 1,
    ReleasedWithConditions = 2,
    Released = 3,
    Blocked = 4
}

public enum CommissioningCheckResult
{
    Open = 0,
    Passed = 1,
    Failed = 2,
    NotApplicable = 3
}

public sealed record CommissioningProfile(
    int MachineNumber,
    string LogoOrderNumber,
    string LogoType,
    string SupplyVoltage,
    string CycleInput,
    string CycleSignal,
    string ValveOutput,
    string ValveVoltage,
    bool UseInterfaceRelay,
    bool EndPositionMonitoring,
    string EndPositionInput,
    ushort DefaultValvePulseMs,
    CommissioningReleaseState ReleaseState,
    string Notes,
    DateTime UpdatedAtUtc);

public sealed record CommissioningCheckRecord(
    int MachineNumber,
    string CheckCode,
    CommissioningCheckResult Result,
    string Note,
    DateTime? CheckedAtUtc);

public sealed record MachineCommunicationDiagnostics(
    int MachineNumber,
    bool SessionExists,
    bool PollingEnabled,
    ConnectionState ConnectionState,
    ushort PcHeartbeat,
    ushort LocalCommandSequence,
    bool CommandSequenceSynchronized,
    ushort AckSequence,
    ushort LogoHeartbeat,
    ushort StatusWord,
    ushort ErrorCode,
    ushort CompletionSequence,
    ushort ActiveCavitiesEcho,
    uint CurrentParts,
    uint TotalCycles,
    ushort CurrentVeNumber,
    ushort CompletedVes,
    DateTime? LastSnapshotUtc,
    string? LastMessage);

public sealed class CommissioningCheckRow : INotifyPropertyChanged
{
    private CommissioningCheckResult _result;
    private string _note = string.Empty;
    private DateTime? _checkedAtUtc;

    public required string Code { get; init; }
    public required string Group { get; init; }
    public required string Description { get; init; }
    public required string AcceptanceCriteria { get; init; }

    public CommissioningCheckResult Result
    {
        get => _result;
        set
        {
            if (_result == value) return;
            _result = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ResultText));
        }
    }

    public string Note
    {
        get => _note;
        set
        {
            if (_note == value) return;
            _note = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public DateTime? CheckedAtUtc
    {
        get => _checkedAtUtc;
        set
        {
            if (_checkedAtUtc == value) return;
            _checkedAtUtc = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CheckedAtText));
        }
    }

    public string ResultText => Result switch
    {
        CommissioningCheckResult.Passed => "BESTANDEN",
        CommissioningCheckResult.Failed => "NICHT BESTANDEN",
        CommissioningCheckResult.NotApplicable => "N. A.",
        _ => "OFFEN"
    };

    public string CheckedAtText => CheckedAtUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") ?? "–";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
