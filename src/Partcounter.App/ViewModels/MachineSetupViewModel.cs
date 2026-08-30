using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Data.Sqlite;
using Partcounter.Services;

namespace Partcounter.ViewModels;

public sealed class MachineSetupViewModel : INotifyPropertyChanged
{
    private readonly DatabaseService _database = new();
    private string _statusText = "Maschinenkonfiguration noch nicht geladen.";

    public MachineSetupViewModel()
    {
        ReloadCommand = new AsyncRelayCommand(_ => ReloadAsync());
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync());
        ApplyRecommendedPlanCommand = new RelayCommand(_ => ApplyRecommendedPlan());
    }

    public ObservableCollection<MachineConfigurationEditRow> Machines { get; } = new();
    public ICommand ReloadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ApplyRecommendedPlanCommand { get; }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public async Task InitializeAsync() => await ReloadAsync();

    private async Task ReloadAsync()
    {
        try
        {
            Machines.Clear();
            foreach (var machine in await _database.LoadMachinesAsync())
            {
                Machines.Add(new MachineConfigurationEditRow
                {
                    MachineNumber = machine.MachineNumber,
                    Name = machine.Name,
                    IpAddress = machine.IpAddress,
                    Port = machine.Port,
                    UnitId = machine.UnitId,
                    Enabled = machine.Enabled
                });
            }

            StatusText = $"{Machines.Count} Maschinen geladen. Änderungen werden nach 'Speichern' beim nächsten Partcounter-Start aktiv.";
        }
        catch (Exception ex)
        {
            StatusText = $"Maschinenkonfiguration konnte nicht geladen werden: {ex.Message}";
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            Validate();

            await _database.ExecuteExclusiveWriteAsync(async connection =>
            {
                await using var transaction = await connection.BeginTransactionAsync();
                foreach (var machine in Machines)
                {
                    var command = connection.CreateCommand();
                    command.Transaction = (SqliteTransaction)transaction;
                    command.CommandText = """
                        UPDATE Machines
                        SET Name=$name, IpAddress=$ip, Port=$port, UnitId=$unit, Enabled=$enabled
                        WHERE MachineNumber=$number;
                        """;
                    command.Parameters.AddWithValue("$name", machine.Name.Trim());
                    command.Parameters.AddWithValue("$ip", machine.IpAddress.Trim());
                    command.Parameters.AddWithValue("$port", machine.Port);
                    command.Parameters.AddWithValue("$unit", machine.UnitId);
                    command.Parameters.AddWithValue("$enabled", machine.Enabled ? 1 : 0);
                    command.Parameters.AddWithValue("$number", machine.MachineNumber);
                    await command.ExecuteNonQueryAsync();
                }
                await transaction.CommitAsync();
            });
            StatusText =
                "Maschinen-/Modbus-Konfiguration gespeichert. Partcounter neu starten, damit alle Kommunikationsworker die neuen Endpunkte verwenden.";
        }
        catch (Exception ex)
        {
            StatusText = $"Maschinenkonfiguration konnte nicht gespeichert werden: {ex.Message}";
        }
    }

    private void Validate()
    {
        var duplicateIps = Machines
            .Where(m => m.Enabled)
            .GroupBy(m => m.IpAddress.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateIps.Count > 0)
            throw new InvalidOperationException($"Doppelte IP-Adresse(n): {string.Join(", ", duplicateIps)}");

        foreach (var machine in Machines)
        {
            if (string.IsNullOrWhiteSpace(machine.Name))
                throw new InvalidOperationException($"M{machine.MachineNumber:00}: Maschinenname fehlt.");
            if (!IPAddress.TryParse(machine.IpAddress.Trim(), out _))
                throw new InvalidOperationException($"M{machine.MachineNumber:00}: Ungültige IP-Adresse '{machine.IpAddress}'.");
            if (machine.Port is < 1 or > 65535)
                throw new InvalidOperationException($"M{machine.MachineNumber:00}: Port muss zwischen 1 und 65535 liegen.");
            if (machine.UnitId is < 1 or > 247)
                throw new InvalidOperationException($"M{machine.MachineNumber:00}: Unit-ID muss zwischen 1 und 247 liegen.");
        }
    }

    private void ApplyRecommendedPlan()
    {
        foreach (var machine in Machines)
        {
            machine.IpAddress = $"192.168.50.{100 + machine.MachineNumber}";
            machine.Port = 502;
            machine.UnitId = 1;
            machine.Enabled = true;
        }

        StatusText =
            "Empfohlenen Partcounter-IP-Plan gesetzt: LOGO! M01–M30 = 192.168.50.101–130, Port 502, Unit-ID 1. " +
            "Empfehlung für den Partcounter-PC: 192.168.50.10/24. Erst nach Prüfung Ihrer realen Netzstruktur speichern.";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    public sealed class MachineConfigurationEditRow : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _ipAddress = string.Empty;
        private int _port = 502;
        private byte _unitId = 1;
        private bool _enabled = true;

        public int MachineNumber { get; init; }

        public string Name
        {
            get => _name;
            set { if (_name == value) return; _name = value ?? string.Empty; OnPropertyChanged(); }
        }

        public string IpAddress
        {
            get => _ipAddress;
            set { if (_ipAddress == value) return; _ipAddress = value ?? string.Empty; OnPropertyChanged(); }
        }

        public int Port
        {
            get => _port;
            set { if (_port == value) return; _port = value; OnPropertyChanged(); }
        }

        public byte UnitId
        {
            get => _unitId;
            set { if (_unitId == value) return; _unitId = value; OnPropertyChanged(); }
        }

        public bool Enabled
        {
            get => _enabled;
            set { if (_enabled == value) return; _enabled = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

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
