using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Partcounter.Models;
using Partcounter.ViewModels;
using Partcounter.Views;

namespace Partcounter;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private CompactMonitorWindow? _compactMonitor;
    private MachineSetupViewModel? _machineSetupViewModel;
    private LabelDesignerViewModel? _labelDesignerViewModel;
    private CommissioningViewModel? _commissioningViewModel;
    private CommissioningFleetOverviewViewModel? _commissioningFleetOverviewViewModel;
    private AlsViewModel? _alsViewModel;
    private TabItem? _machineSetupTab;
    private TabItem? _labelDesignerTab;
    private TabItem? _commissioningTab;
    private TabItem? _commissioningFleetTab;
    private TabItem? _alsTab;
    private TextBlock? _versionStatusTextBlock;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Partcounter R001.10";
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnMainViewModelPropertyChanged;
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

            _viewModel.VisibleMachines.CollectionChanged += OnVisibleMachinesChanged;
            MachineItemsControl.ItemContainerGenerator.StatusChanged += OnMachineContainerStatusChanged;

            _compactMonitor = new CompactMonitorWindow(_viewModel, this);

            _machineSetupViewModel = new MachineSetupViewModel();
            await _machineSetupViewModel.InitializeAsync();
            _machineSetupTab = new TabItem
            {
                Header = "Maschinen / Modbus",
                Content = new MachineSetupView { DataContext = _machineSetupViewModel }
            };
            MainTabs.Items.Insert(Math.Max(0, MainTabs.Items.Count - 1), _machineSetupTab);

            _labelDesignerViewModel = new LabelDesignerViewModel(_viewModel);
            await _labelDesignerViewModel.InitializeAsync();
            _labelDesignerTab = new TabItem
            {
                Header = "Etiketteneditor",
                Content = new LabelDesignerView { DataContext = _labelDesignerViewModel }
            };
            MainTabs.Items.Insert(Math.Max(0, MainTabs.Items.Count - 1), _labelDesignerTab);

            _commissioningViewModel = new CommissioningViewModel(_viewModel);
            await _commissioningViewModel.InitializeAsync();
            _commissioningTab = new TabItem
            {
                Header = "Inbetriebnahme / Diagnose",
                Content = new CommissioningView { DataContext = _commissioningViewModel }
            };
            MainTabs.Items.Insert(Math.Max(0, MainTabs.Items.Count - 1), _commissioningTab);

            _commissioningFleetOverviewViewModel = new CommissioningFleetOverviewViewModel(_viewModel);
            await _commissioningFleetOverviewViewModel.InitializeAsync();
            _commissioningFleetTab = new TabItem
            {
                Header = "Rolloutstatus 30 Maschinen",
                Content = new CommissioningFleetOverviewView { DataContext = _commissioningFleetOverviewViewModel }
            };
            MainTabs.Items.Insert(Math.Max(0, MainTabs.Items.Count - 1), _commissioningFleetTab);

            _alsViewModel = new AlsViewModel(_viewModel);
            await _alsViewModel.InitializeAsync();
            _alsTab = new TabItem
            {
                Header = "ARBURG ALS",
                Content = new AlsIntegrationView { DataContext = _alsViewModel }
            };
            MainTabs.Items.Insert(Math.Max(0, MainTabs.Items.Count - 1), _alsTab);

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                AttachMachineContextMenus();
                LocateVersionStatusTextBlock();
                RefreshVersionStatusText();
            }));
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Partcounter konnte nicht initialisiert werden:\n\n{ex.Message}",
                "Partcounter R001.10",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsSimulationMode) or nameof(MainViewModel.SystemStatusText))
            RefreshVersionStatusText();
    }

    private void LocateVersionStatusTextBlock()
    {
        _versionStatusTextBlock = FindBoundTextBlock(this, "SystemStatusText");
        if (_versionStatusTextBlock is not null)
            BindingOperations.ClearBinding(_versionStatusTextBlock, TextBlock.TextProperty);
    }

    private void RefreshVersionStatusText()
    {
        if (_versionStatusTextBlock is null)
            return;

        _versionStatusTextBlock.Text = _viewModel.IsSimulationMode
            ? "R001.10 · SIMULATION"
            : "R001.10 · ECHTBETRIEB MODBUS TCP";
    }

    private static TextBlock? FindBoundTextBlock(DependencyObject root, string bindingPath)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock textBlock)
            {
                var expression = BindingOperations.GetBindingExpression(textBlock, TextBlock.TextProperty);
                if (expression?.ParentBinding.Path?.Path == bindingPath)
                    return textBlock;
            }

            var nested = FindBoundTextBlock(child, bindingPath);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private void OnVisibleMachinesChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(AttachMachineContextMenus));

    private void OnMachineContainerStatusChanged(object? sender, EventArgs e)
    {
        if (MachineItemsControl.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(AttachMachineContextMenus));
    }

    private void AttachMachineContextMenus()
    {
        foreach (var machine in _viewModel.VisibleMachines)
        {
            if (MachineItemsControl.ItemContainerGenerator.ContainerFromItem(machine) is not FrameworkElement container)
                continue;

            container.ContextMenu = CreateMachineContextMenu(machine);
        }
    }

    private ContextMenu CreateMachineContextMenu(MachineState machine)
    {
        var menu = new ContextMenu();
        var pauseItem = new MenuItem { Header = "Auftrag pausieren" };
        var resumeItem = new MenuItem { Header = "Auftrag fortsetzen" };
        var endItem = new MenuItem { Header = "Auftrag beenden" };
        var disableItem = new MenuItem();

        pauseItem.Click += (_, _) => ExecuteMachineCommand(machine, _viewModel.PauseOrderCommand);
        resumeItem.Click += (_, _) => ExecuteMachineCommand(machine, _viewModel.ResumeOrderCommand);
        endItem.Click += (_, _) => ExecuteMachineCommand(machine, _viewModel.EndOrderCommand);
        disableItem.Click += (_, _) => ExecuteMachineCommand(machine, _viewModel.ToggleMachineDisabledCommand);

        menu.Items.Add(pauseItem);
        menu.Items.Add(resumeItem);
        menu.Items.Add(endItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(disableItem);

        menu.Opened += (_, _) =>
        {
            _viewModel.SelectedMachine = machine;
            pauseItem.IsEnabled = machine.OrderState == ProductionOrderState.Running && !machine.IsTemporarilyDisabled;
            resumeItem.IsEnabled = machine.OrderState == ProductionOrderState.Paused && !machine.IsTemporarilyDisabled;
            endItem.IsEnabled = machine.IsActiveOrder;
            disableItem.Header = machine.IsTemporarilyDisabled ? "Maschine wieder aktivieren" : "Temporär deaktivieren";
        };

        return menu;
    }

    private void ExecuteMachineCommand(MachineState machine, ICommand command)
    {
        _viewModel.SelectedMachine = machine;
        if (command.CanExecute(null))
            command.Execute(null);
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

        _viewModel.PropertyChanged -= OnMainViewModelPropertyChanged;
        _viewModel.VisibleMachines.CollectionChanged -= OnVisibleMachinesChanged;
        MachineItemsControl.ItemContainerGenerator.StatusChanged -= OnMachineContainerStatusChanged;

        _alsViewModel?.Dispose();
        _alsViewModel = null;
        _commissioningFleetOverviewViewModel?.Dispose();
        _commissioningFleetOverviewViewModel = null;
        _commissioningViewModel?.Dispose();
        _commissioningViewModel = null;
        _labelDesignerViewModel = null;
        _machineSetupViewModel = null;

        if (_compactMonitor is not null)
        {
            _compactMonitor.ShutdownMonitor();
            _compactMonitor = null;
        }

        await _viewModel.DisposeAsync();
    }
}
