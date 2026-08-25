using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Partcounter.Models;
using Partcounter.Services;

namespace Partcounter.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly DatabaseService _database = new();
    private readonly LabelPrintService _labelPrinter = new();
    private readonly MachineFleetService _fleet = new();
    private readonly DispatcherTimer _simulationTimer;

    private bool _isSimulationMode = true;
    private MachineState? _selectedMachine;
    private ArticleDefinition? _selectedArticle;
    private string _orderNumber = $"AUF-{DateTime.Now:yyyyMMdd}-001";
    private string _statusMessage = "Initialisierung …";
    private string _labelPrinterName = string.Empty;
    private bool _autoPrintLabels = true;
    private ushort _valvePulseMs = 750;

    private string _editArticleNumber = string.Empty;
    private string _editArticleDescription = string.Empty;
    private string _editToolNumber = string.Empty;
    private ushort _editCavities = 1;
    private uint _editPackagingQuantity = 1000;

    public MainViewModel()
    {
        ApplyArticleCommand = new AsyncRelayCommand(_ => ApplySelectedArticleAsync());
        SaveArticleCommand = new AsyncRelayCommand(_ => SaveArticleAsync());
        ToggleOperatingModeCommand = new AsyncRelayCommand(_ => ToggleOperatingModeAsync());
        AddCycleCommand = new RelayCommand(machine => (machine as MachineState)?.ApplySimulationCycle());
        ManualVeChangeCommand = new AsyncRelayCommand(ManualVeChangeAsync);
        ResetMachineCommand = new AsyncRelayCommand(ResetMachineAsync);
        SavePrintSettingsCommand = new AsyncRelayCommand(_ => SavePrintSettingsAsync());
        TestLabelCommand = new AsyncRelayCommand(_ => TestLabelAsync());
        CopySelectedArticleToEditorCommand = new RelayCommand(_ => CopySelectedArticleToEditor());

        _fleet.SnapshotReceived += FleetOnSnapshotReceived;
        _fleet.ConnectionChanged += FleetOnConnectionChanged;

        _simulationTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _simulationTimer.Tick += (_, _) => SimulationTick();
        _simulationTimer.Start();
    }

    public ObservableCollection<MachineState> Machines { get; } = new();
    public ObservableCollection<ArticleDefinition> Articles { get; } = new();
    public ObservableCollection<PackagingUnitRecord> RecentPackagingUnits { get; } = new();

    public ICommand ApplyArticleCommand { get; }
    public ICommand SaveArticleCommand { get; }
    public ICommand ToggleOperatingModeCommand { get; }
    public ICommand AddCycleCommand { get; }
    public ICommand ManualVeChangeCommand { get; }
    public ICommand ResetMachineCommand { get; }
    public ICommand SavePrintSettingsCommand { get; }
    public ICommand TestLabelCommand { get; }
    public ICommand CopySelectedArticleToEditorCommand { get; }

    public MachineState? SelectedMachine
    {
        get => _selectedMachine;
        set => SetField(ref _selectedMachine, value);
    }

    public ArticleDefinition? SelectedArticle
    {
        get => _selectedArticle;
        set => SetField(ref _selectedArticle, value);
    }

    public string OrderNumber
    {
        get => _orderNumber;
        set => SetField(ref _orderNumber, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool IsSimulationMode
    {
        get => _isSimulationMode;
        private set
        {
            if (!SetField(ref _isSimulationMode, value)) return;
            OnPropertyChanged(nameof(OperatingModeButtonText));
            OnPropertyChanged(nameof(SystemStatusText));
        }
    }

    public string OperatingModeButtonText => IsSimulationMode ? "Echtbetrieb aktivieren" : "Simulation aktivieren";
    public string SystemStatusText => IsSimulationMode ? "R001 · SIMULATION" : "R001 · ECHTBETRIEB MODBUS TCP";
    public string DatabasePath => _database.DatabasePath;

    public string LabelPrinterName
    {
        get => _labelPrinterName;
        set => SetField(ref _labelPrinterName, value);
    }

    public bool AutoPrintLabels
    {
        get => _autoPrintLabels;
        set => SetField(ref _autoPrintLabels, value);
    }

    public ushort ValvePulseMs
    {
        get => _valvePulseMs;
        set => SetField(ref _valvePulseMs, value);
    }

    public string EditArticleNumber
    {
        get => _editArticleNumber;
        set => SetField(ref _editArticleNumber, value);
    }

    public string EditArticleDescription
    {
        get => _editArticleDescription;
        set => SetField(ref _editArticleDescription, value);
    }

    public string EditToolNumber
    {
        get => _editToolNumber;
        set => SetField(ref _editToolNumber, value);
    }

    public ushort EditCavities
    {
        get => _editCavities;
        set => SetField(ref _editCavities, value);
    }

    public uint EditPackagingQuantity
    {
        get => _editPackagingQuantity;
        set => SetField(ref _editPackagingQuantity, value);
    }

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        Machines.Clear();
        foreach (var configuration in await _database.LoadMachinesAsync())
        {
            var machine = new MachineState
            {
                Configuration = configuration,
                SimulatedCycleTimeSeconds = 4.0 + configuration.MachineNumber % 11,
                NextSimulatedCycleLocal = DateTime.Now.AddMilliseconds(configuration.MachineNumber * 90),
                ConnectionState = ConnectionState.Simulation
            };
            machine.VeCompleted += MachineOnVeCompleted;
            Machines.Add(machine);
        }

        await ReloadArticlesAsync();

        RecentPackagingUnits.Clear();
        foreach (var record in await _database.LoadRecentPackagingUnitsAsync())
            RecentPackagingUnits.Add(record);

        LabelPrinterName = await _database.GetSettingAsync("LabelPrinterName") ?? string.Empty;
        AutoPrintLabels = bool.TryParse(await _database.GetSettingAsync("AutoPrintLabels"), out var autoPrint) && autoPrint;

        SelectedMachine = Machines.FirstOrDefault();
        SelectedArticle = Articles.FirstOrDefault();
        StatusMessage = $"Bereit · {Machines.Count} Maschinen · {Articles.Count} Artikel · DB: {_database.DatabasePath}";
    }

    private async Task ToggleOperatingModeAsync()
    {
        if (IsSimulationMode)
        {
            foreach (var machine in Machines)
                machine.ConnectionState = ConnectionState.Offline;

            await _fleet.StartAsync(Machines.Select(m => m.Configuration));
            IsSimulationMode = false;
            StatusMessage = "Echtbetrieb aktiv: Partcounter verbindet parallel mit allen freigegebenen LOGO!-Stationen.";
        }
        else
        {
            await _fleet.StopAsync();
            IsSimulationMode = true;
            foreach (var machine in Machines)
                machine.ConnectionState = ConnectionState.Simulation;
            StatusMessage = "Simulation aktiv. Es werden keine Modbus-Schreibbefehle an LOGO! gesendet.";
        }
    }

    private async Task ApplySelectedArticleAsync()
    {
        if (SelectedMachine is null || SelectedArticle is null)
        {
            StatusMessage = "Bitte Maschine und Artikel auswählen.";
            return;
        }

        if (SelectedArticle.ActiveCavities is < 1 or > 64 || SelectedArticle.PackagingQuantity == 0)
        {
            StatusMessage = "Artikelparameter sind ungültig.";
            return;
        }

        var order = string.IsNullOrWhiteSpace(OrderNumber) ? $"AUF-{DateTime.Now:yyyyMMdd-HHmmss}" : OrderNumber.Trim();
        SelectedMachine.ApplyArticle(SelectedArticle, order, resetCounters: true);

        if (!IsSimulationMode)
        {
            var job = new JobParameters(
                StableUInt32(order),
                SelectedArticle.ArticleNumber,
                SelectedArticle.ToolNumber,
                SelectedArticle.ActiveCavities,
                SelectedArticle.PackagingQuantity,
                SelectedArticle.RequiredCycles,
                ValvePulseMs);

            try
            {
                await _fleet.SendJobAsync(SelectedMachine.Configuration.MachineNumber, job);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Auftrag lokal vorbereitet, Übertragung fehlgeschlagen: {ex.Message}";
                await _database.AddEventAsync(SelectedMachine.Configuration.MachineNumber, "MODBUS_WRITE_ERROR", ex.Message);
                return;
            }
        }

        StatusMessage = $"{SelectedMachine.DisplayName}: {SelectedArticle.ArticleNumber}, {SelectedArticle.ActiveCavities} Kavitäten, VE-Soll {SelectedArticle.PackagingQuantity:N0}, effektiv {SelectedArticle.EffectivePackagingQuantity:N0}.";
        await _database.AddEventAsync(SelectedMachine.Configuration.MachineNumber, "JOB_APPLIED", StatusMessage);
    }

    private async Task ManualVeChangeAsync(object? parameter)
    {
        if (parameter is not MachineState machine) return;

        if (IsSimulationMode)
        {
            machine.CompleteCurrentVe(VeCompletionReason.Manual);
            return;
        }

        try
        {
            await _fleet.SendManualVeChangeAsync(machine.Configuration.MachineNumber);
            StatusMessage = $"Manueller VE-Wechsel an {machine.DisplayName} angefordert.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"VE-Wechsel fehlgeschlagen: {ex.Message}";
        }
    }

    private async Task ResetMachineAsync(object? parameter)
    {
        if (parameter is not MachineState machine) return;

        if (IsSimulationMode)
        {
            machine.ResetCounters();
            return;
        }

        try
        {
            await _fleet.ResetJobAsync(machine.Configuration.MachineNumber);
            StatusMessage = $"Reset an {machine.DisplayName} gesendet.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Reset fehlgeschlagen: {ex.Message}";
        }
    }

    private async Task SaveArticleAsync()
    {
        try
        {
            var article = new ArticleDefinition(
                0,
                EditArticleNumber.Trim(),
                EditArticleDescription.Trim(),
                EditToolNumber.Trim(),
                EditCavities,
                EditPackagingQuantity,
                true);
            await _database.UpsertArticleAsync(article);
            await ReloadArticlesAsync(article.ArticleNumber);
            StatusMessage = $"Artikel {article.ArticleNumber} gespeichert. Effektive VE-Menge: {article.EffectivePackagingQuantity:N0} Stück.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Artikel konnte nicht gespeichert werden: {ex.Message}";
        }
    }

    private async Task ReloadArticlesAsync(string? selectArticleNumber = null)
    {
        Articles.Clear();
        foreach (var article in await _database.LoadArticlesAsync())
            Articles.Add(article);

        if (!string.IsNullOrWhiteSpace(selectArticleNumber))
            SelectedArticle = Articles.FirstOrDefault(a => a.ArticleNumber.Equals(selectArticleNumber, StringComparison.OrdinalIgnoreCase));
        else
            SelectedArticle ??= Articles.FirstOrDefault();
    }

    private void CopySelectedArticleToEditor()
    {
        if (SelectedArticle is null) return;
        EditArticleNumber = SelectedArticle.ArticleNumber;
        EditArticleDescription = SelectedArticle.Description;
        EditToolNumber = SelectedArticle.ToolNumber;
        EditCavities = SelectedArticle.ActiveCavities;
        EditPackagingQuantity = SelectedArticle.PackagingQuantity;
    }

    private async Task SavePrintSettingsAsync()
    {
        await _database.SetSettingAsync("LabelPrinterName", LabelPrinterName.Trim());
        await _database.SetSettingAsync("AutoPrintLabels", AutoPrintLabels.ToString().ToLowerInvariant());
        StatusMessage = "Etikettendruck-Einstellungen gespeichert.";
    }

    private async Task TestLabelAsync()
    {
        if (SelectedMachine is null || SelectedArticle is null)
        {
            StatusMessage = "Für Testetikett bitte Maschine und Artikel auswählen.";
            return;
        }

        var now = DateTime.UtcNow;
        var record = new PackagingUnitRecord(
            $"TEST-{now:yyyyMMddHHmmssfff}",
            SelectedMachine.Configuration.MachineNumber,
            SelectedMachine.Configuration.Name,
            1,
            OrderNumber,
            SelectedArticle.ArticleNumber,
            SelectedArticle.Description,
            SelectedArticle.ToolNumber,
            SelectedArticle.ActiveCavities,
            SelectedArticle.PackagingQuantity,
            SelectedArticle.EffectivePackagingQuantity,
            SelectedArticle.ExpectedOverfill,
            VeCompletionReason.AutomaticFull,
            now,
            "Test",
            null);

        var printed = await _labelPrinter.PrintAsync(record, LabelPrinterName);
        StatusMessage = printed ? "Testetikett an Drucker übergeben." : "Testetikett nicht gedruckt. Druckername prüfen.";
    }

    private void SimulationTick()
    {
        if (!IsSimulationMode) return;

        var now = DateTime.Now;
        foreach (var machine in Machines)
        {
            if (now < machine.NextSimulatedCycleLocal) continue;
            machine.ApplySimulationCycle();
            machine.NextSimulatedCycleLocal = now.AddSeconds(machine.SimulatedCycleTimeSeconds);
        }
    }

    private void FleetOnSnapshotReceived(object? sender, MachineSnapshotEventArgs e)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var machine = Machines.FirstOrDefault(m => m.Configuration.MachineNumber == e.MachineNumber);
            if (machine is null) return;
            machine.ApplyLogoSnapshot(e.Snapshot);
        });
    }

    private void FleetOnConnectionChanged(object? sender, MachineConnectionEventArgs e)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var machine = Machines.FirstOrDefault(m => m.Configuration.MachineNumber == e.MachineNumber);
            if (machine is null) return;
            machine.ConnectionState = e.State;
            if (e.State == ConnectionState.Offline && !string.IsNullOrWhiteSpace(e.Message))
                StatusMessage = $"{machine.DisplayName} offline: {e.Message}";
        });
    }

    private async void MachineOnVeCompleted(object? sender, VeCompletedEventArgs e)
    {
        if (sender is not MachineState machine) return;

        var completedUtc = e.CompletedAtLocal.ToUniversalTime();
        var overfill = e.Quantity > machine.TargetPartsPerVe ? e.Quantity - machine.TargetPartsPerVe : 0;
        var initialStatus = AutoPrintLabels ? "Pending" : "Disabled";
        var record = new PackagingUnitRecord(
            $"PC-{completedUtc:yyyyMMddHHmmssfff}-M{machine.Configuration.MachineNumber:00}-VE{e.VeNumber:0000}",
            machine.Configuration.MachineNumber,
            machine.Configuration.Name,
            e.VeNumber,
            machine.OrderNumber,
            machine.ArticleNumber,
            machine.ArticleDescription,
            machine.ToolNumber,
            machine.ActiveCavities,
            machine.TargetPartsPerVe,
            e.Quantity,
            overfill,
            e.Reason,
            completedUtc,
            initialStatus,
            null);

        try
        {
            await _database.SavePackagingUnitAsync(record);

            if (AutoPrintLabels)
            {
                var printed = await _labelPrinter.PrintAsync(record, LabelPrinterName);
                var printedAt = printed ? DateTime.UtcNow : (DateTime?)null;
                var labelStatus = printed ? "Printed" : "PendingPrinter";
                await _database.UpdateLabelStatusAsync(record.Id, labelStatus, printedAt);
                record = record with { LabelStatus = labelStatus, PrintedAtUtc = printedAt };
            }

            RecentPackagingUnits.Insert(0, record);
            while (RecentPackagingUnits.Count > 100)
                RecentPackagingUnits.RemoveAt(RecentPackagingUnits.Count - 1);

            StatusMessage = $"{machine.DisplayName}: VE {e.VeNumber} fertig mit {e.Quantity:N0} Teilen; Etikett: {record.LabelStatus}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"VE-Abschluss konnte nicht vollständig protokolliert werden: {ex.Message}";
        }
    }

    private static uint StableUInt32(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var c in value)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash == 0 ? 1u : hash;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _simulationTimer.Stop();
        await _fleet.DisposeAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class RelayCommand(Action<object?> execute) : ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute(parameter);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }

    private sealed class AsyncRelayCommand(Func<object?, Task> execute) : ICommand
    {
        private bool _running;
        public bool CanExecute(object? parameter) => !_running;
        public async void Execute(object? parameter)
        {
            if (_running) return;
            _running = true;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            try { await execute(parameter); }
            finally
            {
                _running = false;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler? CanExecuteChanged;
    }
}
