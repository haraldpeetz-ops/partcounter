using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;
using Partcounter.Models;
using Partcounter.Services;

namespace Partcounter.ViewModels;

public sealed class CommissioningViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly MainViewModel _main;
    private readonly DatabaseService _database = new();
    private readonly DispatcherTimer _refreshTimer;
    private MachineState? _selectedMachine;
    private CommissioningCheckRow? _selectedCheck;
    private LogoSnapshot? _lastProbeSnapshot;

    private string _logoOrderNumber = "6ED1052-2MD08-0BA2";
    private string _logoType = "LOGO! 12/24RCEo";
    private string _supplyVoltage = "24 V DC";
    private string _cycleInput = "I1";
    private string _cycleSignal = "24 V DC";
    private string _valveOutput = "Q1";
    private string _valveVoltage = "24 V DC";
    private bool _useInterfaceRelay = true;
    private bool _endPositionMonitoring;
    private string _endPositionInput = "I2";
    private ushort _defaultValvePulseMs = 750;
    private CommissioningReleaseState _releaseState = CommissioningReleaseState.NotTested;
    private string _notes = string.Empty;

    private string _connectionText = "Nicht initialisiert";
    private string _lastReadText = "–";
    private string _pcHeartbeatText = "–";
    private string _logoHeartbeatText = "–";
    private string _sequenceText = "–";
    private string _statusWordText = "–";
    private string _statusBitsText = "–";
    private string _errorText = "–";
    private string _counterText = "–";
    private string _veText = "–";
    private string _probeStatusText = "Noch keine direkte Leseprobe ausgeführt.";
    private string _statusMessage = "Inbetriebnahmezentrum bereit.";
    private string _lastExportPath = string.Empty;

    public CommissioningViewModel(MainViewModel main)
    {
        _main = main;

        RefreshCommand = new RelayCommand(_ => RefreshLiveDiagnostics());
        ProbeReadCommand = new AsyncRelayCommand(_ => ProbeReadAsync());
        SaveProfileCommand = new AsyncRelayCommand(_ => SaveProfileAsync());
        MarkPassedCommand = new AsyncRelayCommand(_ => MarkSelectedCheckAsync(CommissioningCheckResult.Passed));
        MarkFailedCommand = new AsyncRelayCommand(_ => MarkSelectedCheckAsync(CommissioningCheckResult.Failed));
        MarkNotApplicableCommand = new AsyncRelayCommand(_ => MarkSelectedCheckAsync(CommissioningCheckResult.NotApplicable));
        ResetCheckCommand = new AsyncRelayCommand(_ => MarkSelectedCheckAsync(CommissioningCheckResult.Open));
        ExportProtocolCommand = new AsyncRelayCommand(_ => ExportProtocolAsync());

        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(750)
        };
        _refreshTimer.Tick += (_, _) => RefreshLiveDiagnostics();
    }

    public ObservableCollection<MachineState> Machines => _main.Machines;
    public ObservableCollection<CommissioningCheckRow> Checks { get; } = new();
    public IReadOnlyList<CommissioningReleaseState> ReleaseStates { get; } = Enum.GetValues<CommissioningReleaseState>();

    public ICommand RefreshCommand { get; }
    public ICommand ProbeReadCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand MarkPassedCommand { get; }
    public ICommand MarkFailedCommand { get; }
    public ICommand MarkNotApplicableCommand { get; }
    public ICommand ResetCheckCommand { get; }
    public ICommand ExportProtocolCommand { get; }

    public MachineState? SelectedMachine
    {
        get => _selectedMachine;
        set
        {
            if (!SetField(ref _selectedMachine, value)) return;
            _lastProbeSnapshot = null;
            OnPropertyChanged(nameof(EndpointText));
            OnPropertyChanged(nameof(MachineTitle));
            _ = LoadSelectedMachineAsync();
        }
    }

    public CommissioningCheckRow? SelectedCheck
    {
        get => _selectedCheck;
        set => SetField(ref _selectedCheck, value);
    }

    public string MachineTitle => SelectedMachine is null
        ? "Keine Maschine ausgewählt"
        : $"M{SelectedMachine.Configuration.MachineNumber:00} · {SelectedMachine.Configuration.Name}";

    public string EndpointText => SelectedMachine is null
        ? "–"
        : $"{SelectedMachine.Configuration.IpAddress}:{SelectedMachine.Configuration.Port} · Unit-ID {SelectedMachine.Configuration.UnitId}";

    public string LogoOrderNumber { get => _logoOrderNumber; set => SetField(ref _logoOrderNumber, value); }
    public string LogoType { get => _logoType; set => SetField(ref _logoType, value); }
    public string SupplyVoltage { get => _supplyVoltage; set => SetField(ref _supplyVoltage, value); }
    public string CycleInput { get => _cycleInput; set => SetField(ref _cycleInput, value); }
    public string CycleSignal { get => _cycleSignal; set => SetField(ref _cycleSignal, value); }
    public string ValveOutput { get => _valveOutput; set => SetField(ref _valveOutput, value); }
    public string ValveVoltage { get => _valveVoltage; set => SetField(ref _valveVoltage, value); }
    public bool UseInterfaceRelay { get => _useInterfaceRelay; set => SetField(ref _useInterfaceRelay, value); }
    public bool EndPositionMonitoring { get => _endPositionMonitoring; set => SetField(ref _endPositionMonitoring, value); }
    public string EndPositionInput { get => _endPositionInput; set => SetField(ref _endPositionInput, value); }
    public ushort DefaultValvePulseMs { get => _defaultValvePulseMs; set => SetField(ref _defaultValvePulseMs, value); }
    public CommissioningReleaseState ReleaseState { get => _releaseState; set => SetField(ref _releaseState, value); }
    public string Notes { get => _notes; set => SetField(ref _notes, value); }

    public string ConnectionText { get => _connectionText; private set => SetField(ref _connectionText, value); }
    public string LastReadText { get => _lastReadText; private set => SetField(ref _lastReadText, value); }
    public string PcHeartbeatText { get => _pcHeartbeatText; private set => SetField(ref _pcHeartbeatText, value); }
    public string LogoHeartbeatText { get => _logoHeartbeatText; private set => SetField(ref _logoHeartbeatText, value); }
    public string SequenceText { get => _sequenceText; private set => SetField(ref _sequenceText, value); }
    public string StatusWordText { get => _statusWordText; private set => SetField(ref _statusWordText, value); }
    public string StatusBitsText { get => _statusBitsText; private set => SetField(ref _statusBitsText, value); }
    public string ErrorText { get => _errorText; private set => SetField(ref _errorText, value); }
    public string CounterText { get => _counterText; private set => SetField(ref _counterText, value); }
    public string VeText { get => _veText; private set => SetField(ref _veText, value); }
    public string ProbeStatusText { get => _probeStatusText; private set => SetField(ref _probeStatusText, value); }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public string LastExportPath { get => _lastExportPath; private set => SetField(ref _lastExportPath, value); }

    public string ReleaseStateText => ReleaseState switch
    {
        CommissioningReleaseState.InTest => "IN PRÜFUNG",
        CommissioningReleaseState.ReleasedWithConditions => "MIT AUFLAGEN FREIGEGEBEN",
        CommissioningReleaseState.Released => "FREIGEGEBEN",
        CommissioningReleaseState.Blocked => "GESPERRT",
        _ => "NICHT GEPRÜFT"
    };

    public string ChecklistProgressText
    {
        get
        {
            var completed = Checks.Count(c => c.Result is CommissioningCheckResult.Passed or CommissioningCheckResult.NotApplicable);
            var failed = Checks.Count(c => c.Result == CommissioningCheckResult.Failed);
            return $"{completed}/{Checks.Count} abgeschlossen · {failed} nicht bestanden";
        }
    }

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        SelectedMachine = Machines.FirstOrDefault();
        if (SelectedMachine is not null)
            await LoadSelectedMachineAsync();
        _refreshTimer.Start();
    }

    private async Task LoadSelectedMachineAsync()
    {
        var machine = SelectedMachine;
        if (machine is null) return;

        try
        {
            var profile = await _database.LoadCommissioningProfileAsync(machine.Configuration.MachineNumber)
                ?? BuildDefaultProfile(machine.Configuration.MachineNumber);
            ApplyProfile(profile);
            await LoadChecksAsync(machine.Configuration.MachineNumber);
            RefreshLiveDiagnostics();
            StatusMessage = $"Inbetriebnahmedaten für M{machine.Configuration.MachineNumber:00} geladen.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Inbetriebnahmedaten konnten nicht geladen werden: {ex.Message}";
        }
    }

    private void ApplyProfile(CommissioningProfile profile)
    {
        LogoOrderNumber = profile.LogoOrderNumber;
        LogoType = profile.LogoType;
        SupplyVoltage = profile.SupplyVoltage;
        CycleInput = profile.CycleInput;
        CycleSignal = profile.CycleSignal;
        ValveOutput = profile.ValveOutput;
        ValveVoltage = profile.ValveVoltage;
        UseInterfaceRelay = profile.UseInterfaceRelay;
        EndPositionMonitoring = profile.EndPositionMonitoring;
        EndPositionInput = profile.EndPositionInput;
        DefaultValvePulseMs = profile.DefaultValvePulseMs;
        ReleaseState = profile.ReleaseState;
        Notes = profile.Notes;
        OnPropertyChanged(nameof(ReleaseStateText));
    }

    private async Task SaveProfileAsync()
    {
        var machine = SelectedMachine;
        if (machine is null)
        {
            StatusMessage = "Bitte zuerst eine Maschine auswählen.";
            return;
        }

        if (DefaultValvePulseMs < ModbusRegisterMap.MinValvePulseMs || DefaultValvePulseMs > ModbusRegisterMap.MaxValvePulseMs ||
            DefaultValvePulseMs % ModbusRegisterMap.ValvePulseUnitMs != 0)
        {
            StatusMessage = "Ventilimpuls muss 50…5000 ms betragen und im 10-ms-Raster liegen.";
            return;
        }

        var profile = new CommissioningProfile(
            machine.Configuration.MachineNumber,
            LogoOrderNumber.Trim(),
            LogoType.Trim(),
            SupplyVoltage.Trim(),
            CycleInput.Trim(),
            CycleSignal.Trim(),
            ValveOutput.Trim(),
            ValveVoltage.Trim(),
            UseInterfaceRelay,
            EndPositionMonitoring,
            EndPositionInput.Trim(),
            DefaultValvePulseMs,
            ReleaseState,
            Notes.Trim(),
            DateTime.UtcNow);

        try
        {
            await _database.UpsertCommissioningProfileAsync(profile);
            await _database.AddEventAsync(machine.Configuration.MachineNumber, "COMMISSIONING_PROFILE", $"Freigabestatus: {ReleaseState}");
            StatusMessage = "Hardware-/Freigabeprofil gespeichert.";
            OnPropertyChanged(nameof(ReleaseStateText));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Profil konnte nicht gespeichert werden: {ex.Message}";
        }
    }

    private async Task LoadChecksAsync(int machineNumber)
    {
        var stored = (await _database.LoadCommissioningChecksAsync(machineNumber))
            .ToDictionary(c => c.CheckCode, StringComparer.OrdinalIgnoreCase);

        Checks.Clear();
        foreach (var definition in BuildChecklist())
        {
            if (stored.TryGetValue(definition.Code, out var record))
            {
                definition.Result = record.Result;
                definition.Note = record.Note;
                definition.CheckedAtUtc = record.CheckedAtUtc;
            }
            Checks.Add(definition);
        }

        SelectedCheck = Checks.FirstOrDefault();
        OnPropertyChanged(nameof(ChecklistProgressText));
    }

    private async Task MarkSelectedCheckAsync(CommissioningCheckResult result)
    {
        var machine = SelectedMachine;
        var check = SelectedCheck;
        if (machine is null || check is null)
        {
            StatusMessage = "Bitte Maschine und Prüfschritt auswählen.";
            return;
        }

        check.Result = result;
        check.CheckedAtUtc = result == CommissioningCheckResult.Open ? null : DateTime.UtcNow;

        await _database.UpsertCommissioningCheckAsync(new CommissioningCheckRecord(
            machine.Configuration.MachineNumber,
            check.Code,
            check.Result,
            check.Note,
            check.CheckedAtUtc));

        if (ReleaseState == CommissioningReleaseState.NotTested && result != CommissioningCheckResult.Open)
        {
            ReleaseState = CommissioningReleaseState.InTest;
            OnPropertyChanged(nameof(ReleaseStateText));
        }

        OnPropertyChanged(nameof(ChecklistProgressText));
        StatusMessage = $"Prüfschritt {check.Code}: {check.ResultText}.";
    }

    private void RefreshLiveDiagnostics()
    {
        var machine = SelectedMachine;
        if (machine is null) return;

        var diagnostics = MachineFleetService.GetGlobalCommunicationDiagnostics(machine.Configuration.MachineNumber);
        if (diagnostics is null)
        {
            ConnectionText = _main.IsSimulationMode
                ? "Simulation · kein Echtbetrieb-Session"
                : machine.ConnectionState.ToString();

            if (_lastProbeSnapshot is not null)
            {
                ApplySnapshotOnly(_lastProbeSnapshot);
                LastReadText = $"Direkte Probe: {_lastProbeSnapshot.ReadAtUtc.ToLocalTime():dd.MM.yyyy HH:mm:ss}";
            }
            else
            {
                LastReadText = "–";
                PcHeartbeatText = "–";
                LogoHeartbeatText = "–";
                SequenceText = "–";
                StatusWordText = "–";
                StatusBitsText = "–";
                ErrorText = "–";
                CounterText = "–";
                VeText = "–";
            }
            return;
        }

        ConnectionText = diagnostics.ConnectionState switch
        {
            ConnectionState.Online => "ONLINE · Modbus TCP",
            ConnectionState.Offline => $"OFFLINE · {diagnostics.LastMessage ?? "keine Antwort"}",
            ConnectionState.Fault => $"FEHLER · {diagnostics.LastMessage ?? "unbekannt"}",
            _ => diagnostics.ConnectionState.ToString()
        };
        LastReadText = diagnostics.LastSnapshotUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss.fff") ?? "–";
        PcHeartbeatText = diagnostics.PcHeartbeat == 0 ? "–" : diagnostics.PcHeartbeat.ToString();
        LogoHeartbeatText = diagnostics.LogoHeartbeat == 0 ? "–" : diagnostics.LogoHeartbeat.ToString();
        SequenceText = diagnostics.CommandSequenceSynchronized
            ? $"PC {diagnostics.LocalCommandSequence} / ACK {diagnostics.AckSequence}"
            : $"nicht synchronisiert / ACK {diagnostics.AckSequence}";
        ApplyStatus(diagnostics.StatusWord, diagnostics.ErrorCode);
        CounterText = $"VE: {diagnostics.CurrentParts:N0} Teile · Gesamtzyklen: {diagnostics.TotalCycles:N0}";
        VeText = $"VE Nr. {diagnostics.CurrentVeNumber} · fertig {diagnostics.CompletedVes} · Kavitätenecho {diagnostics.ActiveCavitiesEcho}";
    }

    private void ApplySnapshotOnly(LogoSnapshot snapshot)
    {
        LogoHeartbeatText = snapshot.LogoHeartbeat.ToString();
        SequenceText = $"ACK {snapshot.AcknowledgedCommandSequence} · direkte Probe";
        ApplyStatus(snapshot.StatusWord, snapshot.ErrorCode);
        CounterText = $"VE: {snapshot.CurrentParts:N0} Teile · Gesamtzyklen: {snapshot.TotalCycles:N0}";
        VeText = $"VE Nr. {snapshot.CurrentVeNumber} · fertig {snapshot.CompletedVes} · Kavitätenecho {snapshot.ActiveCavitiesEcho}";
    }

    private void ApplyStatus(ushort statusWord, ushort errorCode)
    {
        StatusWordText = $"0x{statusWord:X4} ({statusWord})";
        var bits = new List<string>();
        if ((statusWord & ModbusRegisterMap.StatusReady) != 0) bits.Add("READY");
        if ((statusWord & ModbusRegisterMap.StatusAutomaticEnabled) != 0) bits.Add("AUTO");
        if ((statusWord & ModbusRegisterMap.StatusVeChangeActive) != 0) bits.Add("VE-WECHSEL");
        if ((statusWord & ModbusRegisterMap.StatusAlarm) != 0) bits.Add("ALARM");
        if ((statusWord & ModbusRegisterMap.StatusCycleInputActive) != 0) bits.Add("I1 AKTIV");
        if ((statusWord & ModbusRegisterMap.StatusPcHeartbeatStale) != 0) bits.Add("PC-HB STEHT");
        StatusBitsText = bits.Count == 0 ? "keine Statusbits" : string.Join(" · ", bits);
        ErrorText = BuildErrorText(errorCode);
    }

    private async Task ProbeReadAsync()
    {
        var machine = SelectedMachine;
        if (machine is null)
        {
            ProbeStatusText = "Bitte zuerst eine Maschine auswählen.";
            return;
        }

        ProbeStatusText = $"Leseprobe zu {machine.Configuration.IpAddress}:{machine.Configuration.Port} läuft …";
        try
        {
            await using var client = new LogoModbusClient(machine.Configuration);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            await client.ConnectAsync(cts.Token);
            var snapshot = await client.ReadSnapshotAsync(cts.Token);
            _lastProbeSnapshot = snapshot;
            ApplySnapshotOnly(snapshot);
            LastReadText = $"Direkte Probe: {snapshot.ReadAtUtc.ToLocalTime():dd.MM.yyyy HH:mm:ss}";
            ProbeStatusText = $"OK · Protocol V{ModbusRegisterMap.ProtocolVersion} · ACK {snapshot.AcknowledgedCommandSequence} · LOGO-HB {snapshot.LogoHeartbeat}";
            await _database.AddEventAsync(machine.Configuration.MachineNumber, "COMMISSIONING_PROBE_OK", ProbeStatusText);
        }
        catch (Exception ex)
        {
            ProbeStatusText = $"Leseprobe fehlgeschlagen: {ex.Message}";
            await _database.AddEventAsync(machine.Configuration.MachineNumber, "COMMISSIONING_PROBE_ERROR", ex.Message);
        }
    }

    private async Task ExportProtocolAsync()
    {
        var machine = SelectedMachine;
        if (machine is null)
        {
            StatusMessage = "Bitte zuerst eine Maschine auswählen.";
            return;
        }

        try
        {
            RefreshLiveDiagnostics();
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Partcounter",
                "Inbetriebnahme");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"Partcounter_Inbetriebnahme_M{machine.Configuration.MachineNumber:00}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            var sb = new StringBuilder();
            sb.AppendLine("Bereich;Feld;Wert");
            AddCsv(sb, "Maschine", "Nummer", machine.Configuration.MachineNumber.ToString());
            AddCsv(sb, "Maschine", "Name", machine.Configuration.Name);
            AddCsv(sb, "Netzwerk", "Endpunkt", EndpointText);
            AddCsv(sb, "Hardware", "LOGO Bestellnummer", LogoOrderNumber);
            AddCsv(sb, "Hardware", "LOGO Typ", LogoType);
            AddCsv(sb, "Hardware", "Versorgung", SupplyVoltage);
            AddCsv(sb, "Hardware", "Zykluseingang", $"{CycleInput} / {CycleSignal}");
            AddCsv(sb, "Hardware", "Ventilausgang", $"{ValveOutput} / {ValveVoltage}");
            AddCsv(sb, "Hardware", "Koppelrelais", UseInterfaceRelay ? "Ja" : "Nein");
            AddCsv(sb, "Hardware", "Endlagenüberwachung", EndPositionMonitoring ? $"Ja / {EndPositionInput}" : "Nein");
            AddCsv(sb, "Hardware", "Standard Ventilimpuls", $"{DefaultValvePulseMs} ms");
            AddCsv(sb, "Freigabe", "Status", ReleaseStateText);
            AddCsv(sb, "Freigabe", "Notizen", Notes);
            AddCsv(sb, "Live", "Verbindung", ConnectionText);
            AddCsv(sb, "Live", "Letzte Antwort", LastReadText);
            AddCsv(sb, "Live", "PC Heartbeat", PcHeartbeatText);
            AddCsv(sb, "Live", "LOGO Heartbeat", LogoHeartbeatText);
            AddCsv(sb, "Live", "Sequenz", SequenceText);
            AddCsv(sb, "Live", "StatusWord", StatusWordText);
            AddCsv(sb, "Live", "Statusbits", StatusBitsText);
            AddCsv(sb, "Live", "ErrorCode", ErrorText);
            AddCsv(sb, "Live", "Zähler", CounterText);
            AddCsv(sb, "Live", "VE", VeText);
            sb.AppendLine();
            sb.AppendLine("Prüfcode;Gruppe;Prüfschritt;Akzeptanzkriterium;Ergebnis;Zeit;Notiz");
            foreach (var check in Checks)
            {
                sb.AppendLine(string.Join(";", new[]
                {
                    Csv(check.Code), Csv(check.Group), Csv(check.Description), Csv(check.AcceptanceCriteria),
                    Csv(check.ResultText), Csv(check.CheckedAtText), Csv(check.Note)
                }));
            }

            await File.WriteAllTextAsync(path, sb.ToString(), new UTF8Encoding(true));
            LastExportPath = path;
            StatusMessage = $"Inbetriebnahmeprotokoll exportiert: {path}";
            await _database.AddEventAsync(machine.Configuration.MachineNumber, "COMMISSIONING_EXPORT", path);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Protokoll konnte nicht exportiert werden: {ex.Message}";
        }
    }

    private static void AddCsv(StringBuilder sb, string area, string field, string value) =>
        sb.AppendLine($"{Csv(area)};{Csv(field)};{Csv(value)}");

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    private static string BuildErrorText(ushort errorCode) => errorCode switch
    {
        ModbusRegisterMap.ErrorNone => "0 · kein Fehler",
        ModbusRegisterMap.ErrorProtocolVersion => "1 · falsche Protokollversion",
        ModbusRegisterMap.ErrorInvalidCavities => "2 · ungültige Kavitätenzahl",
        ModbusRegisterMap.ErrorInvalidTargetParts => "3 · TargetPartsPerVE = 0",
        ModbusRegisterMap.ErrorInvalidTargetCycles => "4 · ungültige Zielzyklen",
        ModbusRegisterMap.ErrorInvalidValvePulse => "5 · ungültige Ventilzeit",
        ModbusRegisterMap.ErrorVeChangerTimeout => "10 · Wechsler-Endlage Timeout",
        ModbusRegisterMap.ErrorInternalState => "30 · interner Ablaufzustand ungültig",
        _ => $"{errorCode} · unbekannter Fehlercode"
    };

    private static CommissioningProfile BuildDefaultProfile(int machineNumber) => new(
        machineNumber,
        "6ED1052-2MD08-0BA2",
        "LOGO! 12/24RCEo",
        "24 V DC",
        "I1",
        "24 V DC",
        "Q1",
        "24 V DC",
        true,
        false,
        "I2",
        750,
        CommissioningReleaseState.NotTested,
        machineNumber == 1 ? "Referenzmaschine 01 · kleines Festo-Ventil · Spulendaten bei Inbetriebnahme prüfen." : string.Empty,
        DateTime.UtcNow);

    private static IEnumerable<CommissioningCheckRow> BuildChecklist()
    {
        yield return NewCheck("HW-01", "Hardware", "LOGO!-Typ und Versorgung prüfen", "6ED1052-2MD08-0BA2 / 24 V DC stimmen mit realer Station überein.");
        yield return NewCheck("NET-01", "Netzwerk", "IP, Port und Unit-ID prüfen", "Konfigurierter Endpunkt ist eindeutig und TCP 502 erreichbar.");
        yield return NewCheck("MOD-01", "Modbus", "ProtocolVersion lesen", "HR20 meldet ProtocolVersion 2.");
        yield return NewCheck("HB-01", "Modbus", "PC- und LOGO!-Heartbeat beobachten", "Beide Werte ändern sich zyklisch ohne Stillstandsmeldung.");
        yield return NewCheck("CMD-01", "Modbus", "CommandSequence/AckSequence prüfen", "Jeder neue Befehl wird genau einmal quittiert; Neustart synchronisiert sauber.");
        yield return NewCheck("I1-01", "Zyklus", "24-V-Signal an I1 prüfen", "Signalpegel und gemeinsame 0-V-Referenz sind elektrisch zulässig.");
        yield return NewCheck("I1-02", "Zyklus", "Ein Zyklus = ein Zählschritt", "Genau eine positive Maschinenflanke erhöht den Zykluszähler genau einmal.");
        yield return NewCheck("I1-03", "Zyklus", "Pause/Fortsetzen bei I1 HIGH", "Fortsetzen erzeugt keinen künstlichen zusätzlichen Zyklus.");
        yield return NewCheck("CALC-01", "Zählung", "64-fach-Rundungstest", "1000 Soll / 64 Kavitäten = 16 Zyklen = 1024 effektive Teile.");
        yield return NewCheck("Q1-01", "Ventil", "Q1-Koppelrelais und Absicherung prüfen", "Q1 schaltet Interface-Relais; externer Steuerstromkreis ist abgesichert.");
        yield return NewCheck("Q1-02", "Ventil", "Ventilimpulse messen", "50 / 750 / 5000 ms liegen innerhalb der festgelegten Toleranz.");
        yield return NewCheck("VE-01", "VE-Wechsel", "Automatischen VE-Abschluss prüfen", "CompletionSequence erhöht sich einmal; Q1 liefert genau einen Impuls.");
        yield return NewCheck("VE-02", "VE-Wechsel", "Manuellen VE-Abschluss prüfen", "Teil-VE wird einmal abgeschlossen; LastCompletionReason = 2.");
        yield return NewCheck("COM-01", "Ausfalltest", "PC/WLAN unterbrechen", "LOGO! zählt lokal weiter und führt fälligen VE-Wechsel aus.");
        yield return NewCheck("PWR-01", "Wiederanlauf", "LOGO!-Power-Cycle prüfen", "Q1 bleibt beim Start AUS; Wiederanlauf entspricht freigegebenem Konzept.");
        yield return NewCheck("DOC-01", "Dokumentation", "Etikett und VE-Historie prüfen", "Pro VE genau ein Datensatz/Etikett mit korrekten Mengen und IDs.");
    }

    private static CommissioningCheckRow NewCheck(string code, string group, string description, string criteria) => new()
    {
        Code = code,
        Group = group,
        Description = description,
        AcceptanceCriteria = criteria,
        Result = CommissioningCheckResult.Open
    };

    public void Dispose() => _refreshTimer.Stop();

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        if (propertyName == nameof(ReleaseState))
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReleaseStateText)));
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
