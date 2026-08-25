using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Partcounter.Models;

public sealed record MachineConfiguration(
    int MachineNumber,
    string Name,
    string IpAddress,
    int Port = 502,
    byte UnitId = 1,
    bool Enabled = true);

public sealed record JobParameters(
    uint JobId,
    string ArticleNumber,
    string ToolNumber,
    ushort ActiveCavities,
    uint TargetPartsPerVe,
    ushort ValvePulseMs = 750);

public sealed record LogoSnapshot(
    uint CurrentParts,
    uint TotalCycles,
    ushort CurrentVeNumber,
    ushort CompletedVes,
    uint LastCompletedVeQuantity,
    ushort StatusWord,
    ushort AcknowledgedCommandSequence,
    ushort ActiveCavitiesEcho,
    DateTime ReadAtUtc);

public enum ConnectionState
{
    Simulation,
    Online,
    Offline,
    Fault
}

public sealed class MachineState : INotifyPropertyChanged
{
    private uint _currentParts;
    private uint _totalCycles;
    private ushort _currentVeNumber = 1;
    private ushort _completedVes;
    private uint _lastCompletedVeQuantity;
    private DateTime? _lastCycleLocal;
    private DateTime? _lastVeCompletedLocal;
    private ConnectionState _connectionState = ConnectionState.Simulation;

    public required MachineConfiguration Configuration { get; init; }
    public string ArticleNumber { get; set; } = "ART-TEST";
    public string ToolNumber { get; set; } = "WZ-TEST";
    public ushort ActiveCavities { get; set; } = 1;
    public uint TargetPartsPerVe { get; set; } = 1000;
    public double SimulatedCycleTimeSeconds { get; set; } = 10.0;
    public DateTime NextSimulatedCycleLocal { get; set; } = DateTime.Now;

    public uint CurrentParts
    {
        get => _currentParts;
        private set
        {
            _currentParts = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FillPercent));
            OnPropertyChanged(nameof(FillText));
        }
    }

    public uint TotalCycles
    {
        get => _totalCycles;
        private set { _totalCycles = value; OnPropertyChanged(); }
    }

    public ushort CurrentVeNumber
    {
        get => _currentVeNumber;
        private set { _currentVeNumber = value; OnPropertyChanged(); }
    }

    public ushort CompletedVes
    {
        get => _completedVes;
        private set { _completedVes = value; OnPropertyChanged(); }
    }

    public uint LastCompletedVeQuantity
    {
        get => _lastCompletedVeQuantity;
        private set { _lastCompletedVeQuantity = value; OnPropertyChanged(); }
    }

    public DateTime? LastCycleLocal
    {
        get => _lastCycleLocal;
        private set
        {
            _lastCycleLocal = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LastCycleText));
        }
    }

    public DateTime? LastVeCompletedLocal
    {
        get => _lastVeCompletedLocal;
        private set
        {
            _lastVeCompletedLocal = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LastVeCompletedText));
        }
    }

    public ConnectionState ConnectionState
    {
        get => _connectionState;
        set { _connectionState = value; OnPropertyChanged(); }
    }

    public string DisplayName => $"M{Configuration.MachineNumber:00} · {Configuration.Name}";
    public string Endpoint => $"{Configuration.IpAddress}:{Configuration.Port}";
    public double FillPercent => TargetPartsPerVe == 0 ? 0 : Math.Min(100.0, CurrentParts * 100.0 / TargetPartsPerVe);
    public string FillText => $"{CurrentParts:N0} / {TargetPartsPerVe:N0} Teile";
    public string LastCycleText => LastCycleLocal?.ToString("HH:mm:ss") ?? "–";
    public string LastVeCompletedText => LastVeCompletedLocal?.ToString("HH:mm:ss") ?? "–";

    public uint ProjectedOverfill
    {
        get
        {
            if (ActiveCavities == 0 || TargetPartsPerVe == 0) return 0;
            var cycles = (uint)Math.Ceiling(TargetPartsPerVe / (double)ActiveCavities);
            return cycles * ActiveCavities - TargetPartsPerVe;
        }
    }

    public void ApplySimulationCycle()
    {
        if (ActiveCavities == 0 || TargetPartsPerVe == 0) return;

        TotalCycles++;
        CurrentParts += ActiveCavities;
        LastCycleLocal = DateTime.Now;

        if (CurrentParts >= TargetPartsPerVe)
            CompleteCurrentVe();
    }

    public void CompleteCurrentVe()
    {
        LastCompletedVeQuantity = CurrentParts;
        LastVeCompletedLocal = DateTime.Now;
        CompletedVes++;
        CurrentVeNumber++;
        CurrentParts = 0;
    }

    public void ResetSimulation()
    {
        CurrentParts = 0;
        TotalCycles = 0;
        CurrentVeNumber = 1;
        CompletedVes = 0;
        LastCompletedVeQuantity = 0;
        LastCycleLocal = null;
        LastVeCompletedLocal = null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
