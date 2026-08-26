using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;
using Partcounter.Models;
using Partcounter.Services;

namespace Partcounter.ViewModels;

public sealed class CommissioningFleetOverviewViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly MainViewModel _main;
    private readonly DatabaseService _database = new();
    private readonly DispatcherTimer _timer;
    private string _statusText = "Rolloutübersicht wird geladen …";
    private string _lastExportPath = string.Empty;

    public CommissioningFleetOverviewViewModel(MainViewModel main)
    {
        _main = main;
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAllAsync());
        ExportCommand = new AsyncRelayCommand(_ => ExportAsync());

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _timer.Tick += (_, _) => RefreshLiveStates();
    }

    public ObservableCollection<CommissioningMachineOverviewRow> Rows { get; } = new();
    public ICommand RefreshCommand { get; }
    public ICommand ExportCommand { get; }

    public string StatusText { get => _statusText; private set => SetField(ref _statusText, value); }
    public string LastExportPath { get => _lastExportPath; private set => SetField(ref _lastExportPath, value); }

    public string SummaryText
    {
        get
        {
            var released = Rows.Count(r => r.ReleaseState == CommissioningReleaseState.Released);
            var conditional = Rows.Count(r => r.ReleaseState == CommissioningReleaseState.ReleasedWithConditions);
            var testing = Rows.Count(r => r.ReleaseState == CommissioningReleaseState.InTest);
            var blocked = Rows.Count(r => r.ReleaseState == CommissioningReleaseState.Blocked);
            var notTested = Rows.Count(r => r.ReleaseState == CommissioningReleaseState.NotTested);
            return $"Freigegeben {released} · mit Auflagen {conditional} · in Prüfung {testing} · gesperrt {blocked} · ungeprüft {notTested}";
        }
    }

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        Rows.Clear();
        foreach (var machine in _main.Machines)
            Rows.Add(new CommissioningMachineOverviewRow(machine));
        await RefreshAllAsync();
        _timer.Start();
    }

    private async Task RefreshAllAsync()
    {
        try
        {
            foreach (var row in Rows)
            {
                var profile = await _database.LoadCommissioningProfileAsync(row.MachineNumber);
                var checks = await _database.LoadCommissioningChecksAsync(row.MachineNumber);

                row.ReleaseState = profile?.ReleaseState ?? CommissioningReleaseState.NotTested;
                row.HardwareText = profile is null
                    ? "Standardprofil noch nicht gespeichert"
                    : $"{profile.LogoOrderNumber} · {profile.CycleInput} {profile.CycleSignal} · {profile.ValveOutput} {profile.ValveVoltage}";

                var passed = checks.Count(c => c.Result == CommissioningCheckResult.Passed);
                var na = checks.Count(c => c.Result == CommissioningCheckResult.NotApplicable);
                var failed = checks.Count(c => c.Result == CommissioningCheckResult.Failed);
                row.ChecklistText = $"{passed + na}/16 abgeschlossen · {failed} Fehler";
            }

            RefreshLiveStates();
            OnPropertyChanged(nameof(SummaryText));
            StatusText = $"Rolloutstatus für {Rows.Count} Maschinen aktualisiert.";
        }
        catch (Exception ex)
        {
            StatusText = $"Rolloutstatus konnte nicht aktualisiert werden: {ex.Message}";
        }
    }

    private void RefreshLiveStates()
    {
        foreach (var row in Rows)
        {
            var diagnostics = MachineFleetService.GetGlobalCommunicationDiagnostics(row.MachineNumber);
            if (diagnostics is null)
            {
                row.ConnectionText = _main.IsSimulationMode ? "Simulation" : "keine Session";
                row.LastResponseText = "–";
                row.ErrorText = "–";
                continue;
            }

            row.ConnectionText = diagnostics.ConnectionState == ConnectionState.Online
                ? "ONLINE"
                : diagnostics.ConnectionState.ToString().ToUpperInvariant();
            row.LastResponseText = diagnostics.LastSnapshotUtc?.ToLocalTime().ToString("HH:mm:ss") ?? "–";
            row.ErrorText = diagnostics.ErrorCode == 0 ? "0 · OK" : diagnostics.ErrorCode.ToString();
        }
    }

    private async Task ExportAsync()
    {
        try
        {
            await RefreshAllAsync();
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Partcounter",
                "Inbetriebnahme");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"Partcounter_Rolloutstatus_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            var sb = new StringBuilder();
            sb.AppendLine("Maschine;Name;Endpunkt;Verbindung;LetzteAntwort;Freigabe;Prüfstatus;Hardware;ErrorCode");
            foreach (var row in Rows)
            {
                sb.AppendLine(string.Join(";", new[]
                {
                    Csv(row.MachineNumber.ToString()), Csv(row.Name), Csv(row.Endpoint), Csv(row.ConnectionText),
                    Csv(row.LastResponseText), Csv(row.ReleaseStateText), Csv(row.ChecklistText), Csv(row.HardwareText), Csv(row.ErrorText)
                }));
            }

            await File.WriteAllTextAsync(path, sb.ToString(), new UTF8Encoding(true));
            LastExportPath = path;
            StatusText = $"Rolloutübersicht exportiert: {path}";
        }
        catch (Exception ex)
        {
            StatusText = $"Rolloutübersicht konnte nicht exportiert werden: {ex.Message}";
        }
    }

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    public void Dispose() => _timer.Stop();

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

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

public sealed class CommissioningMachineOverviewRow : INotifyPropertyChanged
{
    private CommissioningReleaseState _releaseState;
    private string _checklistText = "0/16 abgeschlossen · 0 Fehler";
    private string _hardwareText = "–";
    private string _connectionText = "–";
    private string _lastResponseText = "–";
    private string _errorText = "–";

    public CommissioningMachineOverviewRow(MachineState machine)
    {
        MachineNumber = machine.Configuration.MachineNumber;
        Name = machine.Configuration.Name;
        Endpoint = $"{machine.Configuration.IpAddress}:{machine.Configuration.Port} / U{machine.Configuration.UnitId}";
    }

    public int MachineNumber { get; }
    public string Name { get; }
    public string Endpoint { get; }

    public CommissioningReleaseState ReleaseState
    {
        get => _releaseState;
        set
        {
            if (_releaseState == value) return;
            _releaseState = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ReleaseStateText));
        }
    }

    public string ReleaseStateText => ReleaseState switch
    {
        CommissioningReleaseState.InTest => "IN PRÜFUNG",
        CommissioningReleaseState.ReleasedWithConditions => "MIT AUFLAGEN",
        CommissioningReleaseState.Released => "FREIGEGEBEN",
        CommissioningReleaseState.Blocked => "GESPERRT",
        _ => "NICHT GEPRÜFT"
    };

    public string ChecklistText { get => _checklistText; set => SetField(ref _checklistText, value); }
    public string HardwareText { get => _hardwareText; set => SetField(ref _hardwareText, value); }
    public string ConnectionText { get => _connectionText; set => SetField(ref _connectionText, value); }
    public string LastResponseText { get => _lastResponseText; set => SetField(ref _lastResponseText, value); }
    public string ErrorText { get => _errorText; set => SetField(ref _errorText, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
