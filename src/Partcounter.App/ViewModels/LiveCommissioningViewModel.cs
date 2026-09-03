using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;
using Partcounter.Models;
using Partcounter.Services;

namespace Partcounter.ViewModels;

public sealed class LiveCommissioningViewModel : INotifyPropertyChanged, IDisposable
{
    private const int MaxSessionSamples = 20_000;
    private const int MaxDisplayedSamples = 500;

    private readonly MainViewModel _main;
    private readonly DatabaseService _database = new();
    private readonly DispatcherTimer _timer;
    private readonly List<LiveCommissioningSample> _sessionSamples = new();

    private MachineState? _selectedMachine;
    private bool _isRecording;
    private int? _sessionMachineNumber;
    private DateTime? _sessionStartedUtc;
    private string _preflightText = "Live-Prüfung wird initialisiert …";
    private string _currentLiveText = "Noch keine Echtbetriebsdiagnose erfasst.";
    private string _summaryText = "Noch keine Messung vorhanden.";
    private string _statusMessage = $"{AppVersionInfo.RevisionLabel} Live-Abnahme bereit.";
    private string _lastExportPath = string.Empty;

    public LiveCommissioningViewModel(MainViewModel main)
    {
        _main = main;
        _main.PropertyChanged += OnMainPropertyChanged;

        StartRecordingCommand = new AsyncRelayCommand(_ => StartRecordingAsync());
        StopRecordingCommand = new AsyncRelayCommand(_ => StopRecordingAsync("Messung manuell beendet."));
        ClearCommand = new RelayCommand(_ => ClearSession());
        ExportCommand = new AsyncRelayCommand(_ => ExportSessionAsync());
        WriteEvidenceCommand = new AsyncRelayCommand(_ => WriteEvidenceNotesAsync());
        RefreshCommand = new RelayCommand(_ => RefreshLiveState());

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(750)
        };
        _timer.Tick += (_, _) => RefreshLiveState();
    }

    public ObservableCollection<MachineState> Machines => _main.Machines;
    public ObservableCollection<LiveCommissioningSample> Samples { get; } = new();

    public ICommand StartRecordingCommand { get; }
    public ICommand StopRecordingCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand WriteEvidenceCommand { get; }
    public ICommand RefreshCommand { get; }

    public MachineState? SelectedMachine
    {
        get => _selectedMachine;
        set
        {
            if (IsRecording && !ReferenceEquals(_selectedMachine, value))
            {
                StatusMessage = "Maschinenwechsel ist während einer laufenden Messung gesperrt.";
                OnPropertyChanged();
                return;
            }

            if (!SetField(ref _selectedMachine, value))
                return;

            OnPropertyChanged(nameof(MachineTitle));
            OnPropertyChanged(nameof(EndpointText));
            RefreshLiveState();
        }
    }

    public bool IsRecording
    {
        get => _isRecording;
        private set
        {
            if (!SetField(ref _isRecording, value))
                return;
            OnPropertyChanged(nameof(RecordingStateText));
            OnPropertyChanged(nameof(CanSelectMachine));
        }
    }

    public string MachineTitle => SelectedMachine is null
        ? "Keine Maschine ausgewählt"
        : $"M{SelectedMachine.Configuration.MachineNumber:00} · {SelectedMachine.Configuration.Name}";

    public string EndpointText => SelectedMachine is null
        ? "–"
        : $"{SelectedMachine.Configuration.IpAddress}:{SelectedMachine.Configuration.Port} · Unit-ID {SelectedMachine.Configuration.UnitId}";

    public string OperatingModeText => _main.IsSimulationMode
        ? "SIMULATION · Live-Abnahme gesperrt · separater Simulationszustand"
        : $"ECHTBETRIEB · Modbus TCP Protocol V{ModbusRegisterMap.ProtocolVersion} · separater Live-Zustand";

    public string RecordingStateText => IsRecording ? "MESSUNG LÄUFT" : "MESSUNG GESTOPPT";
    public bool CanSelectMachine => !IsRecording;
    public bool HasSession => _sessionSamples.Count > 0;
    public string SampleCountText => $"{_sessionSamples.Count:N0} Messpunkte";
    public string SessionMachineText => _sessionMachineNumber.HasValue ? $"M{_sessionMachineNumber:00}" : "–";
    public string SessionStartedText => _sessionStartedUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") ?? "–";

    public string PreflightText { get => _preflightText; private set => SetField(ref _preflightText, value); }
    public string CurrentLiveText { get => _currentLiveText; private set => SetField(ref _currentLiveText, value); }
    public string SummaryText { get => _summaryText; private set => SetField(ref _summaryText, value); }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public string LastExportPath { get => _lastExportPath; private set => SetField(ref _lastExportPath, value); }

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        SelectedMachine = Machines.FirstOrDefault(m => m.Configuration.MachineNumber == 1) ?? Machines.FirstOrDefault();
        RefreshLiveState();
        _timer.Start();
    }

    private void OnMainPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsSimulationMode)
            or nameof(MainViewModel.SystemStatusText)
            or nameof(MainViewModel.Hf5IsUsingSimulationMachines)
            or nameof(MainViewModel.Hf5IsUsingLiveMachines))
        {
            var previousMachineNumber = _selectedMachine?.Configuration.MachineNumber;

            if (_main.IsSimulationMode && IsRecording)
                _ = StopRecordingAsync("Messung automatisch beendet: Betriebsart wurde auf Simulation umgeschaltet.");

            if (previousMachineNumber.HasValue)
            {
                var activeMachine = Machines.FirstOrDefault(m =>
                    m.Configuration.MachineNumber == previousMachineNumber.Value);
                if (activeMachine is not null && !ReferenceEquals(activeMachine, _selectedMachine))
                {
                    _selectedMachine = activeMachine;
                    OnPropertyChanged(nameof(SelectedMachine));
                    OnPropertyChanged(nameof(MachineTitle));
                    OnPropertyChanged(nameof(EndpointText));
                }
            }
            else
            {
                _selectedMachine = Machines.FirstOrDefault();
                OnPropertyChanged(nameof(SelectedMachine));
                OnPropertyChanged(nameof(MachineTitle));
                OnPropertyChanged(nameof(EndpointText));
            }

            OnPropertyChanged(nameof(OperatingModeText));
            RefreshLiveState();
        }
    }

    private async Task StartRecordingAsync()
    {
        if (SelectedMachine is null)
        {
            StatusMessage = "Bitte zuerst eine Maschine auswählen.";
            return;
        }

        if (_main.IsSimulationMode)
        {
            StatusMessage = "Live-Abnahme kann nur im Echtbetrieb gestartet werden. Die HF5-Betriebsart wird aus Sicherheitsgründen nicht automatisch umgeschaltet.";
            return;
        }

        if (!_main.Hf5IsUsingLiveMachines)
        {
            StatusMessage = "Live-Abnahme gesperrt: Die Hauptansicht verwendet noch nicht den isolierten Live-Maschinensatz.";
            return;
        }

        if (IsRecording)
        {
            StatusMessage = "Die Live-Abnahmemessung läuft bereits.";
            return;
        }

        _sessionSamples.Clear();
        Samples.Clear();
        _sessionMachineNumber = SelectedMachine.Configuration.MachineNumber;
        _sessionStartedUtc = DateTime.UtcNow;
        LastExportPath = string.Empty;
        SummaryText = "Messung gestartet – Zusammenfassung wird laufend aktualisiert.";
        IsRecording = true;
        RaiseSessionProperties();

        CaptureSample();
        await _database.AddEventAsync(_sessionMachineNumber, "COMMISSIONING_LIVE_START", $"{AppVersionInfo.RevisionLabel} read-only Live-Abnahmemessung gestartet.");
        StatusMessage = $"Live-Abnahmemessung für M{_sessionMachineNumber:00} gestartet. Es werden keine Modbus-Register geschrieben.";
    }

    private async Task StopRecordingAsync(string reason)
    {
        if (!IsRecording)
        {
            if (HasSession)
                UpdateSummary();
            return;
        }

        CaptureSample();
        IsRecording = false;
        UpdateSummary();
        await _database.AddEventAsync(_sessionMachineNumber, "COMMISSIONING_LIVE_STOP", $"{reason} {_sessionSamples.Count} Messpunkte.");
        StatusMessage = reason;
    }

    private void ClearSession()
    {
        if (IsRecording)
        {
            StatusMessage = "Eine laufende Messung muss vor dem Leeren beendet werden.";
            return;
        }

        _sessionSamples.Clear();
        Samples.Clear();
        _sessionMachineNumber = null;
        _sessionStartedUtc = null;
        SummaryText = "Noch keine Messung vorhanden.";
        LastExportPath = string.Empty;
        RaiseSessionProperties();
        StatusMessage = "Messdaten aus der Ansicht gelöscht. Bereits exportierte Dateien und Prüfnotizen bleiben unverändert.";
    }

    private void RefreshLiveState()
    {
        var machine = SelectedMachine;
        if (machine is null)
        {
            PreflightText = "Keine Maschine ausgewählt.";
            CurrentLiveText = "–";
            return;
        }

        var diagnostics = MachineFleetService.GetGlobalCommunicationDiagnostics(machine.Configuration.MachineNumber);
        if (_main.IsSimulationMode)
        {
            PreflightText = "SIMULATION aktiv. HF5 hält die Live-Maschinen- und Recovery-Zustände separat; für die reale Abnahme bewusst auf Echtbetrieb umschalten.";
        }
        else if (!_main.Hf5IsUsingLiveMachines)
        {
            PreflightText = "Echtbetrieb-Anzeige aktiv, aber der isolierte Live-Maschinensatz ist noch nicht vollständig übernommen.";
        }
        else if (diagnostics is null)
        {
            PreflightText = $"Echtbetrieb aktiv, aber für M{machine.Configuration.MachineNumber:00} liegt noch keine Fleet-Diagnosesession vor. IP/Port, Aktivierung und LOGO!-Erreichbarkeit prüfen.";
        }
        else
        {
            PreflightText = diagnostics.ConnectionState switch
            {
                ConnectionState.Online => $"Bereit: ONLINE · Protocol V{ModbusRegisterMap.ProtocolVersion} · letzte LOGO!-Antwort {FormatSnapshotTime(diagnostics.LastSnapshotUtc)} · Command/Ack {(diagnostics.CommandSequenceSynchronized ? "synchron" : "NICHT synchron")}",
                ConnectionState.Offline => $"Nicht bereit: OFFLINE · {diagnostics.LastMessage ?? "keine Antwort"}",
                ConnectionState.Fault => $"Nicht bereit: FEHLER · {diagnostics.LastMessage ?? "unbekannt"}",
                _ => $"Status: {diagnostics.ConnectionState}"
            };
        }

        if (diagnostics is not null)
        {
            CurrentLiveText = $"PC-HB {diagnostics.PcHeartbeat} · LOGO-HB {diagnostics.LogoHeartbeat} · Seq {diagnostics.LocalCommandSequence}/{diagnostics.AckSequence} · " +
                              $"Zyklen {diagnostics.TotalCycles:N0} · VE {diagnostics.CurrentVeNumber} · CompletionSeq {diagnostics.CompletionSequence} · Error {diagnostics.ErrorCode}";
        }
        else
        {
            CurrentLiveText = $"Maschinenstatus: {machine.ConnectionState}";
        }

        if (IsRecording)
            CaptureSample();
    }

    private void CaptureSample()
    {
        if (!IsRecording || SelectedMachine is null || _sessionMachineNumber != SelectedMachine.Configuration.MachineNumber)
            return;

        var machine = SelectedMachine;
        var diagnostics = MachineFleetService.GetGlobalCommunicationDiagnostics(machine.Configuration.MachineNumber);
        var now = DateTime.UtcNow;

        LiveCommissioningSample sample;
        if (diagnostics is null)
        {
            sample = new LiveCommissioningSample(
                now,
                false,
                machine.ConnectionState,
                0,
                0,
                false,
                0,
                0,
                0,
                0,
                0,
                0,
                machine.CurrentParts,
                machine.TotalCycles,
                machine.CurrentVeNumber,
                machine.CompletedVes,
                null,
                "keine Fleet-Diagnose",
                "Keine Kommunikationsdiagnose verfügbar");
        }
        else
        {
            sample = new LiveCommissioningSample(
                now,
                true,
                diagnostics.ConnectionState,
                diagnostics.PcHeartbeat,
                diagnostics.LocalCommandSequence,
                diagnostics.CommandSequenceSynchronized,
                diagnostics.AckSequence,
                diagnostics.LogoHeartbeat,
                diagnostics.StatusWord,
                diagnostics.ErrorCode,
                diagnostics.CompletionSequence,
                diagnostics.ActiveCavitiesEcho,
                diagnostics.CurrentParts,
                diagnostics.TotalCycles,
                diagnostics.CurrentVeNumber,
                diagnostics.CompletedVes,
                diagnostics.LastSnapshotUtc,
                BuildStatusText(diagnostics.StatusWord),
                diagnostics.LastMessage);
        }

        _sessionSamples.Add(sample);
        Samples.Insert(0, sample);
        while (Samples.Count > MaxDisplayedSamples)
            Samples.RemoveAt(Samples.Count - 1);

        RaiseSessionProperties();
        if (_sessionSamples.Count % 4 == 0)
            UpdateSummary();

        if (_sessionSamples.Count >= MaxSessionSamples)
            _ = StopRecordingAsync($"Messung automatisch beendet: Sicherheitsgrenze von {MaxSessionSamples:N0} Messpunkten erreicht.");
    }

    private void UpdateSummary()
    {
        var summary = BuildSummary();
        SummaryText = summary is null ? "Noch keine Messung vorhanden." : summary.DisplayText;
    }

    private LiveCommissioningSummary? BuildSummary()
    {
        if (_sessionSamples.Count == 0)
            return null;

        var valid = _sessionSamples.Where(s => s.DiagnosticsAvailable).ToList();
        var firstValid = valid.FirstOrDefault();
        var lastValid = valid.LastOrDefault();

        var drops = 0;
        var recoveries = 0;
        ConnectionState? previous = null;
        foreach (var sample in _sessionSamples)
        {
            if (previous == ConnectionState.Online && sample.ConnectionState is ConnectionState.Offline or ConnectionState.Fault)
                drops++;
            if (previous is ConnectionState.Offline or ConnectionState.Fault && sample.ConnectionState == ConnectionState.Online)
                recoveries++;
            previous = sample.ConnectionState;
        }

        var duration = _sessionSamples.Count > 1
            ? _sessionSamples[^1].TimestampUtc - _sessionSamples[0].TimestampUtc
            : TimeSpan.Zero;

        var pcHeartbeatChanged = valid.Where(s => s.PcHeartbeat != 0).Select(s => s.PcHeartbeat).Distinct().Take(2).Count() > 1;
        var logoHeartbeatChanged = valid.Where(s => s.LogoHeartbeat != 0).Select(s => s.LogoHeartbeat).Distinct().Take(2).Count() > 1;

        return new LiveCommissioningSummary(
            _sessionSamples.Count,
            duration,
            _sessionSamples.Count(s => s.ConnectionState == ConnectionState.Online),
            _sessionSamples.Count(s => s.ConnectionState == ConnectionState.Offline),
            _sessionSamples.Count(s => s.ConnectionState == ConnectionState.Fault),
            drops,
            recoveries,
            pcHeartbeatChanged,
            logoHeartbeatChanged,
            valid.Count(s => !s.CommandSequenceSynchronized),
            firstValid is null || lastValid is null ? 0 : (long)lastValid.TotalCycles - firstValid.TotalCycles,
            firstValid is null || lastValid is null ? 0 : (long)lastValid.CompletionSequence - firstValid.CompletionSequence,
            firstValid is null || lastValid is null ? 0 : (long)lastValid.CompletedVes - firstValid.CompletedVes,
            valid.Count(s => (s.StatusWord & ModbusRegisterMap.StatusAlarm) != 0),
            valid.Count(s => (s.StatusWord & ModbusRegisterMap.StatusPcHeartbeatStale) != 0),
            valid.Count(s => (s.StatusWord & ModbusRegisterMap.StatusCycleInputActive) != 0),
            firstValid?.PcHeartbeat ?? 0,
            lastValid?.PcHeartbeat ?? 0,
            firstValid?.LogoHeartbeat ?? 0,
            lastValid?.LogoHeartbeat ?? 0);
    }

    private async Task ExportSessionAsync()
    {
        if (IsRecording)
        {
            StatusMessage = "Messung zuerst beenden, damit ein abgeschlossener Datensatz exportiert wird.";
            return;
        }

        if (!HasSession || !_sessionMachineNumber.HasValue)
        {
            StatusMessage = "Keine Live-Abnahmemessung zum Export vorhanden.";
            return;
        }

        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Partcounter",
                "Inbetriebnahme");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"Partcounter_R00125_HF5_LiveAbnahme_M{_sessionMachineNumber:00}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            var summary = BuildSummary();

            var sb = new StringBuilder();
            sb.AppendLine($"PARTCOUNTER {AppVersionInfo.RevisionLabel} LIVE-ABNAHMEMESSUNG");
            AddCsv(sb, "Revision", AppVersionInfo.RevisionLabel);
            AddCsv(sb, "Protocol", $"Modbus TCP V{ModbusRegisterMap.ProtocolVersion}");
            AddCsv(sb, "Maschine", $"M{_sessionMachineNumber:00}");
            AddCsv(sb, "Start lokal", SessionStartedText);
            AddCsv(sb, "Messpunkte", _sessionSamples.Count.ToString());
            AddCsv(sb, "Auswertung", summary?.DisplayText ?? "–");
            AddCsv(sb, "Sicherheitsprinzip", "Read-only Beobachtung der isolierten Live-Fleet-Diagnose; keine Modbus-Schreibbefehle, keine direkte Q1-Ansteuerung.");
            sb.AppendLine();
            sb.AppendLine("Zeit_lokal;Diagnose;Verbindung;PC_HB;LOGO_HB;CommandSeq;AckSeq;Seq_sync;StatusWord;Status;ErrorCode;CompletionSeq;Kavitaeten;CurrentParts;TotalCycles;CurrentVE;CompletedVEs;SourceSnapshot_UTC;Meldung");

            foreach (var sample in _sessionSamples)
            {
                sb.AppendLine(string.Join(";", new[]
                {
                    Csv(sample.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff")),
                    Csv(sample.DiagnosticsAvailable ? "ja" : "nein"),
                    Csv(sample.ConnectionState.ToString()),
                    Csv(sample.PcHeartbeat.ToString()),
                    Csv(sample.LogoHeartbeat.ToString()),
                    Csv(sample.LocalCommandSequence.ToString()),
                    Csv(sample.AckSequence.ToString()),
                    Csv(sample.CommandSequenceSynchronized ? "ja" : "nein"),
                    Csv($"0x{sample.StatusWord:X4}"),
                    Csv(sample.StatusText),
                    Csv(sample.ErrorCode.ToString()),
                    Csv(sample.CompletionSequence.ToString()),
                    Csv(sample.ActiveCavities.ToString()),
                    Csv(sample.CurrentParts.ToString()),
                    Csv(sample.TotalCycles.ToString()),
                    Csv(sample.CurrentVeNumber.ToString()),
                    Csv(sample.CompletedVes.ToString()),
                    Csv(sample.SourceSnapshotUtc?.ToString("O") ?? string.Empty),
                    Csv(sample.Message)
                }));
            }

            await File.WriteAllTextAsync(path, sb.ToString(), new UTF8Encoding(true));
            LastExportPath = path;
            await _database.AddEventAsync(_sessionMachineNumber, "COMMISSIONING_LIVE_EXPORT", path);
            StatusMessage = $"Live-Abnahmemessung exportiert: {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export fehlgeschlagen: {ex.Message}";
        }
    }

    private async Task WriteEvidenceNotesAsync()
    {
        if (IsRecording)
        {
            StatusMessage = "Messung zuerst beenden, bevor Messdaten in die Prüfnotizen übernommen werden.";
            return;
        }

        if (!HasSession || !_sessionMachineNumber.HasValue)
        {
            StatusMessage = "Keine Messdaten vorhanden.";
            return;
        }

        var summary = BuildSummary();
        if (summary is null)
            return;

        try
        {
            var stored = (await _database.LoadCommissioningChecksAsync(_sessionMachineNumber.Value))
                .ToDictionary(c => c.CheckCode, StringComparer.OrdinalIgnoreCase);
            var valid = _sessionSamples.Where(s => s.DiagnosticsAvailable).ToList();
            var first = valid.FirstOrDefault();
            var last = valid.LastOrDefault();
            var evidencePrefix = $"[{AppVersionInfo.RevisionLabel} Messung {DateTime.Now:dd.MM.yyyy HH:mm:ss}] ";

            var entries = new Dictionary<string, string>
            {
                ["MOD-01"] = $"{summary.OnlineSamples:N0} Online-Messpunkte über die Protocol-V{ModbusRegisterMap.ProtocolVersion}-Diagnose erfasst; ErrorCode zuletzt {last?.ErrorCode ?? 0}.",
                ["HB-01"] = $"PC-HB {summary.FirstPcHeartbeat}→{summary.LastPcHeartbeat} ({(summary.PcHeartbeatChanged ? "Änderung erkannt" : "keine Änderung erkannt")}); LOGO-HB {summary.FirstLogoHeartbeat}→{summary.LastLogoHeartbeat} ({(summary.LogoHeartbeatChanged ? "Änderung erkannt" : "keine Änderung erkannt")}).",
                ["CMD-01"] = $"Command/Ack-Synchronität: {summary.SequenceSyncFailureSamples:N0} unsynchrone Messpunkte; Start {first?.LocalCommandSequence ?? 0}/{first?.AckSequence ?? 0}, Ende {last?.LocalCommandSequence ?? 0}/{last?.AckSequence ?? 0}.",
                ["I1-02"] = $"Gesamtzykluszähler Δ {summary.TotalCycleDelta:+#;-#;0}; I1-Aktiv-Bit in {summary.CycleInputActiveSamples:N0} Messpunkten gesehen. Hinweis: Die exakte 1:1-Zuordnung Maschinenflanke→Zählschritt bleibt vor Ort zu bestätigen, da kurze I1-Pulse beim 750-ms-Sampling übersehen werden können.",
                ["VE-01"] = $"CompletionSequence Δ {summary.CompletionSequenceDelta:+#;-#;0}; CompletedVEs Δ {summary.CompletedVeDelta:+#;-#;0}. Die elektrische/physische Q1-Impulsdauer und der reale Kistenwechsel bleiben separat vor Ort zu messen.",
                ["COM-01"] = $"Verbindungsabbrüche {summary.ConnectionDropCount:N0}, Wiederkehr {summary.RecoveryCount:N0}, Gesamtzyklus Δ {summary.TotalCycleDelta:+#;-#;0}. Für den Ausfalltest ist zusätzlich vor Ort zu bestätigen, dass während der Unterbrechung lokal weitergezählt und ein fälliger VE-Wechsel mechanisch ausgeführt wird."
            };

            foreach (var entry in entries)
            {
                stored.TryGetValue(entry.Key, out var existing);
                var previousNote = existing?.Note?.TrimEnd() ?? string.Empty;
                var note = string.IsNullOrWhiteSpace(previousNote)
                    ? evidencePrefix + entry.Value
                    : previousNote + Environment.NewLine + evidencePrefix + entry.Value;

                await _database.UpsertCommissioningCheckAsync(new CommissioningCheckRecord(
                    _sessionMachineNumber.Value,
                    entry.Key,
                    existing?.Result ?? CommissioningCheckResult.Open,
                    note,
                    existing?.CheckedAtUtc));
            }

            await _database.AddEventAsync(_sessionMachineNumber, "COMMISSIONING_LIVE_EVIDENCE", $"{AppVersionInfo.RevisionLabel}: Messdaten in Prüfnotizen übernommen; Prüfergebnisse unverändert.");
            StatusMessage = $"{AppVersionInfo.RevisionLabel}-Messdaten wurden als objektive Evidenz in die passenden Prüfnotizen übernommen. Bestanden/Nicht bestanden wurde bewusst nicht automatisch geändert.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Messdaten konnten nicht in die Prüfnotizen übernommen werden: {ex.Message}";
        }
    }

    private static string BuildStatusText(ushort statusWord)
    {
        var bits = new List<string>();
        if ((statusWord & ModbusRegisterMap.StatusReady) != 0) bits.Add("READY");
        if ((statusWord & ModbusRegisterMap.StatusAutomaticEnabled) != 0) bits.Add("AUTO");
        if ((statusWord & ModbusRegisterMap.StatusVeChangeActive) != 0) bits.Add("VE-WECHSEL");
        if ((statusWord & ModbusRegisterMap.StatusAlarm) != 0) bits.Add("ALARM");
        if ((statusWord & ModbusRegisterMap.StatusCycleInputActive) != 0) bits.Add("I1");
        if ((statusWord & ModbusRegisterMap.StatusPcHeartbeatStale) != 0) bits.Add("PC-HB STALE");
        return bits.Count == 0 ? "keine Bits" : string.Join(" · ", bits);
    }

    private static string FormatSnapshotTime(DateTime? utc) =>
        utc?.ToLocalTime().ToString("HH:mm:ss.fff") ?? "–";

    private static void AddCsv(StringBuilder sb, string field, string value) =>
        sb.AppendLine($"{Csv(field)};{Csv(value)}");

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    private void RaiseSessionProperties()
    {
        OnPropertyChanged(nameof(HasSession));
        OnPropertyChanged(nameof(SampleCountText));
        OnPropertyChanged(nameof(SessionMachineText));
        OnPropertyChanged(nameof(SessionStartedText));
    }

    public void Dispose()
    {
        _timer.Stop();
        _main.PropertyChanged -= OnMainPropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
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
