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

public sealed record ArticleDefinition(
    long Id,
    string ArticleNumber,
    string Description,
    string ToolNumber,
    ushort ActiveCavities,
    uint PackagingQuantity,
    bool Active = true)
{
    public string DisplayName => $"{ArticleNumber} · {Description}";
    public uint RequiredCycles => ActiveCavities == 0 ? 0 : (uint)Math.Ceiling(PackagingQuantity / (double)ActiveCavities);
    public uint EffectivePackagingQuantity => RequiredCycles * ActiveCavities;
    public uint ExpectedOverfill => EffectivePackagingQuantity >= PackagingQuantity
        ? EffectivePackagingQuantity - PackagingQuantity
        : 0;
}

public sealed record JobParameters(
    uint JobId,
    string ArticleNumber,
    string ToolNumber,
    ushort ActiveCavities,
    uint TargetPartsPerVe,
    uint TargetCyclesPerVe,
    ushort ValvePulseMs = 750);

public enum VeCompletionReason : ushort
{
    Unknown = 0,
    AutomaticFull = 1,
    Manual = 2
}

public sealed record LogoSnapshot(
    uint CurrentParts,
    uint TotalCycles,
    ushort CurrentVeNumber,
    ushort CompletedVes,
    uint LastCompletedVeQuantity,
    ushort StatusWord,
    ushort AcknowledgedCommandSequence,
    ushort ActiveCavitiesEcho,
    ushort LastCompletedVeNumber,
    ushort CompletionSequence,
    ushort LogoHeartbeat,
    ushort ErrorCode,
    VeCompletionReason LastCompletionReason,
    DateTime ReadAtUtc);

public sealed record VeCompletedEventArgs(
    ushort VeNumber,
    uint Quantity,
    VeCompletionReason Reason,
    DateTime CompletedAtLocal);

public sealed record PackagingUnitRecord(
    string Id,
    int MachineNumber,
    string MachineName,
    ushort VeNumber,
    string OrderNumber,
    string ArticleNumber,
    string ArticleDescription,
    string ToolNumber,
    ushort Cavities,
    uint TargetQuantity,
    uint ActualQuantity,
    uint Overfill,
    VeCompletionReason CompletionReason,
    DateTime CompletedAtUtc,
    string LabelStatus,
    DateTime? PrintedAtUtc)
{
    public string QuantityText => $"{ActualQuantity:N0} / Soll {TargetQuantity:N0}";
    public string CompletedAtLocalText => CompletedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss");
}

public enum ConnectionState
{
    Simulation,
    Online,
    Offline,
    Fault
}

public sealed class MachineState : INotifyPropertyChanged
{
    private string _articleNumber = "–";
    private string _articleDescription = string.Empty;
    private string _toolNumber = "–";
    private string _orderNumber = string.Empty;
    private ushort _activeCavities = 1;
    private uint _targetPartsPerVe = 1000;
    private uint _currentParts;
    private uint _totalCycles;
    private ushort _currentVeNumber = 1;
    private ushort _completedVes;
    private uint _lastCompletedVeQuantity;
    private DateTime? _lastCycleLocal;
    private DateTime? _lastVeCompletedLocal;
    private ConnectionState _connectionState = ConnectionState.Simulation;
    private ushort _lastCompletionSequence;
    private bool _logoSnapshotInitialized;
    private ushort _errorCode;

    public required MachineConfiguration Configuration { get; init; }
    public double SimulatedCycleTimeSeconds { get; set; } = 10.0;
    public DateTime NextSimulatedCycleLocal { get; set; } = DateTime.Now;

    public string ArticleNumber
    {
        get => _articleNumber;
        private set { _articleNumber = value; OnPropertyChanged(); }
    }

    public string ArticleDescription
    {
        get => _articleDescription;
        private set { _articleDescription = value; OnPropertyChanged(); }
    }

    public string ToolNumber
    {
        get => _toolNumber;
        private set { _toolNumber = value; OnPropertyChanged(); }
    }

    public string OrderNumber
    {
        get => _orderNumber;
        private set { _orderNumber = value; OnPropertyChanged(); }
    }

    public ushort ActiveCavities
    {
        get => _activeCavities;
        private set
        {
            _activeCavities = value;
            OnPropertyChanged();
            RaiseCalculationProperties();
        }
    }

    public uint TargetPartsPerVe
    {
        get => _targetPartsPerVe;
        private set
        {
            _targetPartsPerVe = value;
            OnPropertyChanged();
            RaiseCalculationProperties();
        }
    }

    public uint CurrentParts
    {
        get => _currentParts;
        private set
        {
            _currentParts = value;
            OnPropertyChanged();
            RaiseCalculationProperties();
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
        set { if (_connectionState == value) return; _connectionState = value; OnPropertyChanged(); }
    }

    public ushort ErrorCode
    {
        get => _errorCode;
        private set { _errorCode = value; OnPropertyChanged(); }
    }

    public string DisplayName => $"M{Configuration.MachineNumber:00} · {Configuration.Name}";
    public string Endpoint => $"{Configuration.IpAddress}:{Configuration.Port}";

    // ProgressBar.Value uses a TwoWay default binding mode in WPF. The setter is intentionally
    // ignored because FillPercent is a calculated display value. This keeps the source read-only
    // by design while preventing WPF from rejecting the binding during template creation.
    public double FillPercent
    {
        get => TargetPartsPerVe == 0 ? 0 : Math.Min(100.0, CurrentParts * 100.0 / TargetPartsPerVe);
        set { }
    }

    public string FillText => $"{CurrentParts:N0} / {TargetPartsPerVe:N0} Teile";
    public string LastCycleText => LastCycleLocal?.ToString("HH:mm:ss") ?? "–";
    public string LastVeCompletedText => LastVeCompletedLocal?.ToString("HH:mm:ss") ?? "–";
    public uint RequiredCyclesPerVe => ActiveCavities == 0 || TargetPartsPerVe == 0 ? 0 : (uint)Math.Ceiling(TargetPartsPerVe / (double)ActiveCavities);
    public uint EffectiveVeQuantity => RequiredCyclesPerVe * ActiveCavities;
    public uint ProjectedOverfill => EffectiveVeQuantity >= TargetPartsPerVe ? EffectiveVeQuantity - TargetPartsPerVe : 0;
    public uint RemainingCycles
    {
        get
        {
            if (ActiveCavities == 0 || TargetPartsPerVe == 0 || CurrentParts >= TargetPartsPerVe) return 0;
            return (uint)Math.Ceiling((TargetPartsPerVe - CurrentParts) / (double)ActiveCavities);
        }
    }

    public void ApplyArticle(ArticleDefinition article, string orderNumber, bool resetCounters)
    {
        ArticleNumber = article.ArticleNumber;
        ArticleDescription = article.Description;
        ToolNumber = article.ToolNumber;
        ActiveCavities = article.ActiveCavities;
        TargetPartsPerVe = article.PackagingQuantity;
        OrderNumber = orderNumber;

        if (resetCounters)
            ResetCounters();
    }

    public void ApplySimulationCycle()
    {
        if (ActiveCavities == 0 || TargetPartsPerVe == 0) return;

        TotalCycles++;
        CurrentParts += ActiveCavities;
        LastCycleLocal = DateTime.Now;

        if (CurrentParts >= TargetPartsPerVe)
            CompleteCurrentVe(VeCompletionReason.AutomaticFull);
    }

    public void CompleteCurrentVe(VeCompletionReason reason)
    {
        if (CurrentParts == 0 && reason == VeCompletionReason.Manual) return;

        var finishedVe = CurrentVeNumber;
        var quantity = CurrentParts;
        var completedAt = DateTime.Now;

        LastCompletedVeQuantity = quantity;
        LastVeCompletedLocal = completedAt;
        CompletedVes++;
        CurrentVeNumber++;
        CurrentParts = 0;

        VeCompleted?.Invoke(this, new VeCompletedEventArgs(finishedVe, quantity, reason, completedAt));
    }

    public void ApplyLogoSnapshot(LogoSnapshot snapshot)
    {
        var previousCycles = TotalCycles;
        CurrentParts = snapshot.CurrentParts;
        TotalCycles = snapshot.TotalCycles;
        CurrentVeNumber = snapshot.CurrentVeNumber;
        CompletedVes = snapshot.CompletedVes;
        LastCompletedVeQuantity = snapshot.LastCompletedVeQuantity;
        ErrorCode = snapshot.ErrorCode;

        if (snapshot.TotalCycles != previousCycles)
            LastCycleLocal = snapshot.ReadAtUtc.ToLocalTime();

        if (_logoSnapshotInitialized && snapshot.CompletionSequence != _lastCompletionSequence)
        {
            LastVeCompletedLocal = snapshot.ReadAtUtc.ToLocalTime();
            VeCompleted?.Invoke(this, new VeCompletedEventArgs(
                snapshot.LastCompletedVeNumber,
                snapshot.LastCompletedVeQuantity,
                snapshot.LastCompletionReason,
                snapshot.ReadAtUtc.ToLocalTime()));
        }

        _lastCompletionSequence = snapshot.CompletionSequence;
        _logoSnapshotInitialized = true;
    }

    public void ResetCounters()
    {
        CurrentParts = 0;
        TotalCycles = 0;
        CurrentVeNumber = 1;
        CompletedVes = 0;
        LastCompletedVeQuantity = 0;
        LastCycleLocal = null;
        LastVeCompletedLocal = null;
        _logoSnapshotInitialized = false;
        _lastCompletionSequence = 0;
    }

    public event EventHandler<VeCompletedEventArgs>? VeCompleted;
    public event PropertyChangedEventHandler? PropertyChanged;

    private void RaiseCalculationProperties()
    {
        OnPropertyChanged(nameof(FillPercent));
        OnPropertyChanged(nameof(FillText));
        OnPropertyChanged(nameof(RequiredCyclesPerVe));
        OnPropertyChanged(nameof(EffectiveVeQuantity));
        OnPropertyChanged(nameof(ProjectedOverfill));
        OnPropertyChanged(nameof(RemainingCycles));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
