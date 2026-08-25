using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Partcounter.Models;
using Partcounter.ViewModels;

namespace Partcounter;

public partial class CompactMonitorWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly MainWindow _mainWindow;
    private bool _allowClose;

    public CompactMonitorWindow(MainViewModel viewModel, MainWindow mainWindow)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _mainWindow = mainWindow;
        DataContext = _viewModel;

        Closing += OnClosing;
        Loaded += (_, _) => PositionAtTopRight();
    }

    public void ShowForMinimized()
    {
        PositionAtTopRight();

        if (!IsVisible)
            Show();

        Topmost = true;
    }

    public void HideMonitor()
    {
        if (IsVisible)
            Hide();
    }

    public void FocusMachine(MachineState machine)
    {
        if (!IsVisible)
            ShowForMinimized();

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                CompactItemsControl.UpdateLayout();
                if (CompactItemsControl.ItemContainerGenerator.ContainerFromItem(machine) is FrameworkElement container)
                    container.BringIntoView();
            }));
    }

    public void ShutdownMonitor()
    {
        _allowClose = true;
        Close();
    }

    private void OnMachineRowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2)
            return;

        if (sender is FrameworkElement { DataContext: MachineState machine })
            _mainWindow.RestoreFromCompact(machine);
    }

    private void PositionAtTopRight()
    {
        var area = SystemParameters.WorkArea;
        Left = Math.Max(area.Left, area.Right - Width - 12);
        Top = area.Top + 12;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
            return;

        e.Cancel = true;
        Hide();
    }
}
