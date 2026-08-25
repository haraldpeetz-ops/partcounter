using System.Windows;
using System.Windows.Threading;
using Partcounter.Models;
using Partcounter.ViewModels;

namespace Partcounter;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.InitializeAsync();
            foreach (var machine in _viewModel.Machines)
                machine.VeCompleted += OnMachineVeCompleted;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Partcounter konnte nicht initialisiert werden:\n\n{ex.Message}",
                "Partcounter R001.3",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnMachineVeCompleted(object? sender, VeCompletedEventArgs e)
    {
        if (sender is not MachineState machine) return;

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() => BringMachineIntoFocus(machine)));
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
            return viewportBounds.Contains(elementBounds.TopLeft) && viewportBounds.Contains(elementBounds.BottomRight);
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

        await _viewModel.DisposeAsync();
    }
}
