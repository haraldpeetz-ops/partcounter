using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using Partcounter.Models;

namespace Partcounter.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly DispatcherTimer _timer;
    private bool _simulationRunning = true;

    public MainViewModel()
    {
        Machines = new ObservableCollection<MachineState>(CreateMachines());

        ToggleSimulationCommand = new RelayCommand(_ => SimulationRunning = !SimulationRunning);
        AddCycleCommand = new RelayCommand(machine => (machine as MachineState)?.ApplySimulationCycle());
        ManualVeChangeCommand = new RelayCommand(machine => (machine as MachineState)?.CompleteCurrentVe());
        ResetMachineCommand = new RelayCommand(machine => (machine as MachineState)?.ResetSimulation());

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _timer.Tick += (_, _) => SimulationTick();
        _timer.Start();
    }

    public ObservableCollection<MachineState> Machines { get; }
    public ICommand ToggleSimulationCommand { get; }
    public ICommand AddCycleCommand { get; }
    public ICommand ManualVeChangeCommand { get; }
    public ICommand ResetMachineCommand { get; }

    public bool SimulationRunning
    {
        get => _simulationRunning;
        set
        {
            if (_simulationRunning == value) return;
            _simulationRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SimulationButtonText));
            OnPropertyChanged(nameof(SystemStatusText));
        }
    }

    public string SimulationButtonText => SimulationRunning ? "Simulation anhalten" : "Simulation starten";
    public string SystemStatusText => SimulationRunning ? "R001 · SIMULATION AKTIV" : "R001 · SIMULATION PAUSIERT";

    private void SimulationTick()
    {
        if (!SimulationRunning) return;

        var now = DateTime.Now;
        foreach (var machine in Machines)
        {
            if (now < machine.NextSimulatedCycleLocal) continue;
            machine.ApplySimulationCycle();
            machine.NextSimulatedCycleLocal = now.AddSeconds(machine.SimulatedCycleTimeSeconds);
        }
    }

    private static IEnumerable<MachineState> CreateMachines()
    {
        ushort[] cavityPattern = [1, 2, 4, 8, 16, 32, 64];

        for (var i = 1; i <= 30; i++)
        {
            var cavities = cavityPattern[(i - 1) % cavityPattern.Length];
            yield return new MachineState
            {
                Configuration = new MachineConfiguration(
                    i,
                    $"Spritzgussmaschine {i:00}",
                    $"192.168.50.{100 + i}"),
                ArticleNumber = $"ART-{1000 + i}",
                ToolNumber = $"WZ-{200 + i}",
                ActiveCavities = cavities,
                TargetPartsPerVe = 1000,
                SimulatedCycleTimeSeconds = 5.0 + (i % 10),
                NextSimulatedCycleLocal = DateTime.Now.AddMilliseconds(i * 120),
                ConnectionState = ConnectionState.Simulation
            };
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class RelayCommand(Action<object?> execute) : ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute(parameter);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
