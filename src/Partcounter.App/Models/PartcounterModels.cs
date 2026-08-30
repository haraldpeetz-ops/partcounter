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
    public string DisplayDescription => BuildDisplayDescription(Description, PackagingQuantity);
    public string DisplayName => $"{ArticleNumber} · {DisplayDescription}";
    public uint RequiredCycles => ActiveCavities == 0 ? 0 : (uint)Math.Ceiling(PackagingQuantity / (double)ActiveCavities);
    public uint EffectivePackagingQuantity => RequiredCycles * ActiveCavities;
    public uint ExpectedOverfill => EffectivePackagingQuantity >= PackagingQuantity
        ? EffectivePackagingQuantity - PackagingQuantity
        : 0;

    private static string BuildDisplayDescription(string description, uint packagingQuantity)
    {
        var text = (description ?? string.Empty).Trim();
        var veIndex = text.LastIndexOf("VE ", StringComparison.OrdinalIgnoreCase);
        if (veIndex >= 0)
            text = text[..veIndex].TrimEnd(' ', '/', '-', '·', ':');

        return string.IsNullOrWhiteSpace(text)
            ? $"VE {packagingQuantity:N0}"
            : $"{text} · VE {packagingQuantity:N0}";
    }
}

public sealed record JobParameters(
    uint JobId,
    string ArticleNumber,
    string ToolNumber,
    ushort ActiveCavities,
    uint TargetPartsPerVe,
    uint TargetCyclesPerVe,
    ushort ValvePulseMs = 750,
    ushort HoldAfterVeNumber = 0);

public enum VeCompletionReason : ushort
{
    Unknown = 0,
    AutomaticFull = 1,
    Manual = 2
}

public enum ProductionOrderState
{
    None = 0,
    Running = 1,
    Paused = 2,
    Completed = 3,
    Ended = 4
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
    DateTime ReadAtUtc,
    ushort HoldAfterVeNumberEcho = 0,
    uint JobIdEcho = 0);

public sealed record VeCompletedEventArgs(
    ushort VeNumber,
    uint TargetQuantity,
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
    private uint _currentVeTargetParts;
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
    private bool _hasVeAttention;
    private CancellationTokenSource? _veAttentionCts;
    private ProductionOrderState _orderState = ProductionOrderState.None;
    private uint _orderTargetQuantity;
    private uint _orderProducedQuantity;
    private bool _isTemporarilyDisabled;

    public required MachineConfiguration Configuration { get; init; }
    public double SimulatedCycleTimeSeconds { get; set; } = 10.0;
    public DateTime NextSimulatedCycleLocal { get; set; } = DateTime.Now;

    public string ArticleNumber
    {
        get => _articleNumber;
        private set { if (_articleNumber == value) return; _articleNumber = value; OnPropertyChanged(); }
    }

    public string ArticleDescription
    {
        get => _articleDescription;
        private set { if (_articleDescription == value) return; _articleDescription = value; OnPropertyChanged(); }
    }

    public string ToolNumber
    {
        get => _toolNumber;
        private set { if (_toolNumber == value) return; _toolNumber = value; OnPropertyChanged(); }
    }

    public string OrderNumber
    {
        get => _orderNumber;
        private set
        {
            if (_orderNumber == value) return;
            _orderNumber = value;
            OnPropertyChanged();
            RaiseOrderProperties();
        }
    }

    public ushort ActiveCavities
    {
        get => _activeCavities;
        private set
        {
            if (_activeCavities == value) return;
            _activeCavities = value;
            OnPropertyChanged();
            RaiseCalculationProperties();
            RaiseOrderProperties();
        }
    }

    public uint TargetPartsPerVe
    {
        get => _targetPartsPerVe;
        private set
        {
            if (_targetPartsPerVe == value) return;
            _targetPartsPerVe = value;
            OnPropertyChanged();
            RaiseCalculationProperties();
            RaiseOrderProperties();
        }
    }

    public uint CurrentVeTargetParts
    {
        get => _currentVeTargetParts;
        private set
        {
            if (_currentVeTargetParts == value) return;
            _currentVeTargetParts = value;
            OnPropertyChanged();
            RaiseCalculationProperties();
        }
    }

    public uint CurrentParts
    {
        get => _currentParts;
        private set
        {
            if (_currentParts == value) return;
            _currentParts = value;
            OnPropertyChanged();
            RaiseCalculationProperties();
        }
    }

    public uint TotalCycles
    {
        get => _totalCycles;
        private set
        {
            if (_totalCycles == value) return;
            _totalCycles = value;
            OnPropertyChanged();
        }
    }

    public ushort CurrentVeNumber
    {
        get => _currentVeNumber;
        private set { if (_currentVeNumber == value) return; _currentVeNumber = value; OnPropertyChanged(); }
    }

    public ushort CompletedVes
    {
        get => _completedVes;
        private set { if (_completedVes == value) return; _completedVes = value; OnPropertyChanged(); }
    }

    public uint LastCompletedVeQuantity
    {
        get => _lastCompletedVeQuantity;
        private set
        {
            if (_lastCompletedVeQuantity == value) return;
            _lastCompletedVeQuantity = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VeAttentionText));
        }
    }

    public DateTime? LastCycleLocal
    {
        get => _lastCycleLocal;
        private set
        {
            if (_lastCycleLocal == value) return;
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
            if (_lastVeCompletedLocal == value) return;
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
        private set { if (_errorCode == value) return; _errorCode = value; OnPropertyChanged(); }
    }

    public bool HasVeAttention
    {
        get => _hasVeAttention;
        private set
        {
            if (_hasVeAttention == value) return;
            _hasVeAttention = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VeAttentionText));
            OnPropertyChanged(nameof(ShouldShowAsActive));
        }
    }

    public ProductionOrderState OrderState
    {
        get => _orderState;
        private set
        {
            if (_orderState == value) return;
            _orderState = value;
            OnPropertyChanged();
            RaiseOrderProperties();
        }
    }

    public uint OrderTargetQuantity
    {
        get => _orderTargetQuantity;
        private set
        {
            if (_orderTargetQuantity == value) return;
            _orderTargetQuantity = value;
            OnPropertyChanged();
            RaiseOrderProperties();
        }
    }

    public uint OrderProducedQuantity
    {
        get => _orderProducedQuantity;
        private set
        {
            if (_orderProducedQuantity == value) return;
            _orderProducedQuantity = value;
            OnPropertyChanged();
            RaiseOrderProperties();
        }
    }

    public bool IsTemporarilyDisabled
    {
        get => _isTemporarilyDisabled;
        private set
        {
            if (_isTemporarilyDisabled == value) return;
            _isTemporarilyDisabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(ShouldShowAsActive));
            OnPropertyChanged(nameof(TemporaryStateText));
        }
    }

    public string DisplayName => IsTemporarilyDisabled
        ? $"M{Configuration.MachineNumber:00} · {Configuration.Name} · DEAKTIVIERT"
        : $"M{Configuration.MachineNumber:00} · {Configuration.Name}";
    public string Endpoint => $"{Configuration.IpAddress}:{Configuration.Port}";

    public bool HasOrder => OrderState != ProductionOrderState.None;
    public bool IsActiveOrder => OrderState is ProductionOrderState.Running or ProductionOrderState.Paused;
    public bool ShouldShowAsActive => !IsTemporarilyDisabled && (IsActiveOrder || HasVeAttention);
    public string TemporaryStateText => IsTemporarilyDisabled ? "TEMPORÄR DEAKTIVIERT" : string.Empty;

    public string OrderStatusText => OrderState switch
    {
        ProductionOrderState.Running => "AUFTRAG LÄUFT",
        ProductionOrderState.Paused => "AUFTRAG PAUSIERT",
        ProductionOrderState.Completed => "AUFTRAG ABGESCHLOSSEN",
        ProductionOrderState.Ended => "AUFTRAG BEENDET",
        _ => "KEIN AUFTRAG"
    };

    public double FillPercent
    {
        get => CurrentVeTargetParts == 0 ? 0 : Math.Min(100.0, CurrentParts * 100.0 / CurrentVeTargetParts);
        set { }
    }

    public string FillText => CurrentVeTargetParts == 0
        ? "– / – Teile"
        : $"{CurrentParts:N0} / {CurrentVeTargetParts:N0} Teile";

    public string LastCycleText => LastCycleLocal?.ToString("HH:mm:ss") ?? "–";
    public string LastVeCompletedText => LastVeCompletedLocal?.ToString("HH:mm:ss") ?? "–";
    public string VeAttentionText => HasVeAttention
        ? $"VE VOLL · {LastCompletedVeQuantity:N0} Teile · Wechsel ausgelöst"
        : string.Empty;

    public uint RequiredCyclesPerVe => ActiveCavities == 0 || CurrentVeTargetParts == 0
        ? 0
        : (uint)Math.Ceiling(CurrentVeTargetParts / (double)ActiveCavities);

    public uint EffectiveVeQuantity => RequiredCyclesPerVe * ActiveCavities;
    public uint ProjectedOverfill => EffectiveVeQuantity >= CurrentVeTargetParts
        ? EffectiveVeQuantity - CurrentVeTargetParts
        : 0;

    public uint RemainingCycles
    {
        get
        {
            if (ActiveCavities == 0 || CurrentVeTargetParts == 0 || CurrentParts >= CurrentVeTargetParts)
                return 0;
            return (uint)Math.Ceiling((CurrentVeTargetParts - CurrentParts) / (double)ActiveCavities);
        }
    }

    public uint RemainingOrderQuantity => OrderTargetQuantity > OrderProducedQuantity
        ? OrderTargetQuantity - OrderProducedQuantity
        : 0;

    public double OrderProgressPercent => OrderTargetQuantity == 0
        ? 0
        : Math.Min(100.0, OrderProducedQuantity * 100.0 / OrderTargetQuantity);

    public string OrderProgressText => OrderTargetQuantity == 0
        ? "Kein Auftrag"
        : $"{OrderProducedQuantity:N0} / {OrderTargetQuantity:N0} Teile";

    public uint RequiredOrderVes => TargetPartsPerVe == 0 || OrderTargetQuantity == 0
        ? 0
        : (uint)Math.Ceiling(OrderTargetQuantity / (double)TargetPartsPerVe);

    public string OrderVeText => RequiredOrderVes == 0
        ? "–"
        : $"{CompletedVes:N0} / ca. {RequiredOrderVes:N0} VE";

    public void StartOrder(ArticleDefinition article, string orderNumber, uint orderTargetQuantity)
    {
        if (orderTargetQuantity == 0)
            throw new ArgumentOutOfRangeException(nameof(orderTargetQuantity));

        ClearVeAttention();

        ArticleNumber = article.ArticleNumber;
        ArticleDescription = article.Description;
        ToolNumber = article.ToolNumber;
        ActiveCavities = article.ActiveCavities;
        TargetPartsPerVe = article.PackagingQuantity;
        OrderNumber = orderNumber;
        OrderTargetQuantity = orderTargetQuantity;
        OrderProducedQuantity = 0;
        OrderState = ProductionOrderState.Running;
        IsTemporarilyDisabled = false;

        ResetCountersCore();
        PrepareNextVeTarget();
    }

    public void PauseOrder()
    {
        if (OrderState == ProductionOrderState.Running)
            OrderState = ProductionOrderState.Paused;
    }

    public void ResumeOrder()
    {
        if (OrderState == ProductionOrderState.Paused && !IsTemporarilyDisabled)
            OrderState = ProductionOrderState.Running;
    }

    public void EndOrder()
    {
        if (OrderState is ProductionOrderState.Running or ProductionOrderState.Paused)
            OrderState = ProductionOrderState.Ended;
        CurrentVeTargetParts = 0;
    }

    public void SetTemporarilyDisabled(bool disabled)
    {
        IsTemporarilyDisabled = disabled;
        if (disabled && OrderState == ProductionOrderState.Running)
            OrderState = ProductionOrderState.Paused;
    }

    public void ApplySimulationCycle()
    {
        if (OrderState != ProductionOrderState.Running || IsTemporarilyDisabled)
            return;
        if (ActiveCavities == 0 || CurrentVeTargetParts == 0)
            return;

        TotalCycles++;
        CurrentParts += ActiveCavities;
        OrderProducedQuantity += ActiveCavities;
        LastCycleLocal = DateTime.Now;

        if (CurrentParts >= CurrentVeTargetParts)
            CompleteCurrentVe(VeCompletionReason.AutomaticFull);
    }

    public void CompleteCurrentVe(VeCompletionReason reason)
    {
        if (CurrentParts == 0 && reason == VeCompletionReason.Manual)
            return;

        var finishedVe = CurrentVeNumber;
        var targetQuantity = CurrentVeTargetParts;
        var quantity = CurrentParts;
        var completedAt = DateTime.Now;

        LastCompletedVeQuantity = quantity;
        LastVeCompletedLocal = completedAt;
        CompletedVes++;
        CurrentVeNumber++;
        CurrentParts = 0;
        TriggerVeAttention();

        AdvanceOrderAfterVeCompletion();

        VeCompleted?.Invoke(this, new VeCompletedEventArgs(finishedVe, targetQuantity, quantity, reason, completedAt));
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

        if (IsActiveOrder)
            OrderProducedQuantity = snapshot.TotalCycles * ActiveCavities;

        if (snapshot.TotalCycles != previousCycles)
            LastCycleLocal = snapshot.ReadAtUtc.ToLocalTime();

        if (_logoSnapshotInitialized && snapshot.CompletionSequence != _lastCompletionSequence)
        {
            var targetQuantity = CurrentVeTargetParts;
            LastVeCompletedLocal = snapshot.ReadAtUtc.ToLocalTime();
            TriggerVeAttention();
            AdvanceOrderAfterVeCompletion();

            VeCompleted?.Invoke(this, new VeCompletedEventArgs(
                snapshot.LastCompletedVeNumber,
                targetQuantity,
                snapshot.LastCompletedVeQuantity,
                snapshot.LastCompletionReason,
                snapshot.ReadAtUtc.ToLocalTime()));
        }

        _lastCompletionSequence = snapshot.CompletionSequence;
        _logoSnapshotInitialized = true;
    }

    public void ResetCounters()
    {
        ResetCountersCore();
        PrepareNextVeTarget();
    }

    public event EventHandler<VeCompletedEventArgs>? VeCompleted;
    public event PropertyChangedEventHandler? PropertyChanged;

    private void ResetCountersCore()
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
        OrderProducedQuantity = 0;
        ClearVeAttention();
    }

    private void AdvanceOrderAfterVeCompletion()
    {
        if (OrderTargetQuantity > 0 && OrderProducedQuantity >= OrderTargetQuantity)
        {
            OrderState = ProductionOrderState.Completed;
            CurrentVeTargetParts = 0;
            return;
        }

        PrepareNextVeTarget();
    }

    private void PrepareNextVeTarget()
    {
        if (!IsActiveOrder || TargetPartsPerVe == 0 || ActiveCavities == 0)
        {
            CurrentVeTargetParts = 0;
            return;
        }

        var remaining = RemainingOrderQuantity;
        if (remaining == 0)
        {
            CurrentVeTargetParts = 0;
            return;
        }

        CurrentVeTargetParts = Math.Min(TargetPartsPerVe, remaining);
    }

    private void TriggerVeAttention()
    {
        _veAttentionCts?.Cancel();
        _veAttentionCts?.Dispose();
        _veAttentionCts = new CancellationTokenSource();
        HasVeAttention = true;
        _ = ClearVeAttentionLaterAsync(_veAttentionCts.Token);
    }

    private async Task ClearVeAttentionLaterAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
                HasVeAttention = false;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ClearVeAttention()
    {
        _veAttentionCts?.Cancel();
        _veAttentionCts?.Dispose();
        _veAttentionCts = null;
        HasVeAttention = false;
    }

    private void RaiseCalculationProperties()
    {
        OnPropertyChanged(nameof(FillPercent));
        OnPropertyChanged(nameof(FillText));
        OnPropertyChanged(nameof(RequiredCyclesPerVe));
        OnPropertyChanged(nameof(EffectiveVeQuantity));
        OnPropertyChanged(nameof(ProjectedOverfill));
        OnPropertyChanged(nameof(RemainingCycles));
    }

    private void RaiseOrderProperties()
    {
        OnPropertyChanged(nameof(HasOrder));
        OnPropertyChanged(nameof(IsActiveOrder));
        OnPropertyChanged(nameof(ShouldShowAsActive));
        OnPropertyChanged(nameof(OrderStatusText));
        OnPropertyChanged(nameof(RemainingOrderQuantity));
        OnPropertyChanged(nameof(OrderProgressPercent));
        OnPropertyChanged(nameof(OrderProgressText));
        OnPropertyChanged(nameof(RequiredOrderVes));
        OnPropertyChanged(nameof(OrderVeText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
