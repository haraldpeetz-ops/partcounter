using System.Windows;
using System.Windows.Threading;
using Partcounter.Models;
using Partcounter.ViewModels;

namespace Partcounter;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private CompactMonitorWindow? _compactMonitor;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
        StateChanged += OnStateChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.InitializeAsync();

            foreach (var machine in _viewModel.Machines)
                machine.VeCompleted += OnMachineVeCompleted;

            _compactMonitor = new CompactMonitorWindow(_viewModel, this);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Partcounter konnte nicht initialisiert werden:\n\n{ex.Message}",
                "Partcounter R001.5",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (_compactMonitor is null)
            return;

        if (WindowState == WindowState.Minimized)
            _compactMonitor.ShowForMinimized();
        else
            _compactMonitor.HideMonitor();
    }

    private void OnMachineVeCompleted(object? sender, VeCompletedEventArgs e)
    {
        if (sender is not MachineState machine) return;

        _viewModel.SelectedMachine = machine;

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                if (WindowState == WindowState.Minimized)
                    _compactMonitor?.FocusMachine(machine);
                else
                    BringMachineIntoFocus(machine);
            }));
    }

    internal void RestoreFromCompact(MachineState? machine)
    {
        if (machine is not null)
            _viewModel.SelectedMachine = machine;

        WindowState = WindowState.Normal;
        Show();
        Activate();

        if (machine is not null)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() => BringMachineIntoFocus(machine)));
        }
    }

    private void BringMachineIntoFocus(MachineState machine)
    {
        _viewModel.SelectedMachine = machine;
        MainTabs.SelectedIndex = 0;
        MachineItemsControl.UpdateLayout();

        if (MachineItemsControl.ItemContainerGenerator.ContainerFromItem(machine) is not FrameworkElement container)
            return;

        if (!IsFullyVisible(container, MachineScrollViewer))
            container.BringIntoView();
    }

    private static bool IsFullyVisible(FrameworkElement element, FrameworkElement viewport)
    {
        try
        {
            var origin = element.TransformToAncestor(viewport).Transform(new Point(0, 0));
            var elementBounds = new Rect(origin, new Size(element.ActualWidth, element.ActualHeight));
            var viewportBounds = new Rect(0, 0, viewport.ActualWidth, viewport.ActualHeight);
            return viewportBounds.Contains(elementBounds.TopLeft) &&
                   viewportBounds.Contains(elementBounds.BottomRight);
        }
        catch
        {
            return false;
        }
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        foreach (var machine in _viewModel.Machines)
            machine.VeCompleted -= OnMachineVeCompleted;

        if (_compactMonitor is not null)
        {
            _compactMonitor.ShutdownMonitor();
            _compactMonitor = null;
        }

        await _viewModel.DisposeAsync();
    }
}
