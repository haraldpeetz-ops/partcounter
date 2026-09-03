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
using Partcounter.Services;
using Partcounter.ViewModels;
using Partcounter.Views;

namespace Partcounter;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly AdminAccessService _adminAccess = new();
    private readonly Dictionary<TabItem, string> _protectedTabs = new();

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
    private TabItem? _lastAllowedTab;
    private TextBlock? _versionStatusTextBlock;
    private Button? _operatingModeButton;
    private Button? _adminButton;
    private bool _tabGuardBusy;
    private bool _machineContextMenuAttachPending;

    // Stable access for bootstraps whose views are moved into the nested
    // administration tab after startup. Looking the tab up in MainTabs is no
    // longer reliable once the administration hub has been built.
    internal CommissioningView? CommissioningView => _commissioningTab?.Content as CommissioningView;

    public MainWindow()
    {
        InitializeComponent();
        Title = AppVersionInfo.ProductTitle;
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnMainViewModelPropertyChanged;
        _adminAccess.StateChanged += OnAdminAccessStateChanged;
        MainTabs.SelectionChanged += OnMainTabSelectionChanged;
        Loaded += OnLoaded;
        Closed += OnClosed;
        StateChanged += OnStateChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _adminAccess.Initialize();
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
            RegisterProtectedTab(_machineSetupTab, "Maschinen / Modbus");

            _labelDesignerViewModel = new LabelDesignerViewModel(_viewModel);
            await _labelDesignerViewModel.InitializeAsync();
            _labelDesignerTab = new TabItem
            {
                Header = "Etiketteneditor",
                Content = new LabelDesignerView { DataContext = _labelDesignerViewModel }
            };
            MainTabs.Items.Insert(Math.Max(0, MainTabs.Items.Count - 1), _labelDesignerTab);
            RegisterProtectedTab(_labelDesignerTab, "Etiketteneditor");

            _commissioningViewModel = new CommissioningViewModel(_viewModel);
            await _commissioningViewModel.InitializeAsync();
            _commissioningTab = new TabItem
            {
                Header = "Inbetriebnahme / Diagnose",
                Content = new CommissioningView { DataContext = _commissioningViewModel }
            };
            MainTabs.Items.Insert(Math.Max(0, MainTabs.Items.Count - 1), _commissioningTab);
            RegisterProtectedTab(_commissioningTab, "Inbetriebnahme / Diagnose");

            _commissioningFleetOverviewViewModel = new CommissioningFleetOverviewViewModel(_viewModel);
            await _commissioningFleetOverviewViewModel.InitializeAsync();
            _commissioningFleetTab = new TabItem
            {
                Header = "Rolloutstatus 30 Maschinen",
                Content = new CommissioningFleetOverviewView { DataContext = _commissioningFleetOverviewViewModel }
            };
            MainTabs.Items.Insert(Math.Max(0, MainTabs.Items.Count - 1), _commissioningFleetTab);
            RegisterProtectedTab(_commissioningFleetTab, "Rolloutstatus 30 Maschinen");

            _alsViewModel = new AlsViewModel(_viewModel);
            await _alsViewModel.InitializeAsync();
            _alsTab = new TabItem
            {
                Header = "ARBURG ALS",
                Content = new AlsIntegrationView { DataContext = _alsViewModel }
            };
            MainTabs.Items.Insert(Math.Max(0, MainTabs.Items.Count - 1), _alsTab);
            RegisterProtectedTab(_alsTab, "ARBURG ALS");

            var settingsTab = MainTabs.Items
                .OfType<TabItem>()
                .FirstOrDefault(t => string.Equals(t.Header?.ToString(), "Einstellungen / Druck", StringComparison.Ordinal));
            if (settingsTab is not null)
                RegisterProtectedTab(settingsTab, "Einstellungen / Druck");

            _lastAllowedTab = MainTabs.Items.OfType<TabItem>().FirstOrDefault(t => !_protectedTabs.ContainsKey(t));

            _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                AttachMachineContextMenus();
                LocateVersionStatusTextBlock();
                AttachAdminControls();
                RefreshVersionStatusText();
                RefreshAdminUi();
            }));
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Partcounter konnte nicht initialisiert werden:\n\n{ex.Message}",
                AppVersionInfo.ProductTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void RegisterProtectedTab(TabItem tab, string displayName)
    {
        _protectedTabs[tab] = displayName;
        RefreshAdminUi();
    }

    private void OnMainTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_tabGuardBusy || !ReferenceEquals(e.Source, MainTabs) || MainTabs.SelectedItem is not TabItem selectedTab)
            return;

        if (!_protectedTabs.ContainsKey(selectedTab) || _adminAccess.IsUnlocked)
        {
            _lastAllowedTab = selectedTab;
            return;
        }

        _tabGuardBusy = true;
        try
        {
            var requestedTab = selectedTab;
            var fallback = _lastAllowedTab ?? MainTabs.Items.OfType<TabItem>().FirstOrDefault(t => !_protectedTabs.ContainsKey(t));
            if (fallback is not null)
                MainTabs.SelectedItem = fallback;

            if (EnsureAdminUnlocked())
            {
                MainTabs.SelectedItem = requestedTab;
                _lastAllowedTab = requestedTab;
            }
        }
        finally
        {
            _tabGuardBusy = false;
        }
    }

    private bool EnsureAdminUnlocked()
    {
        if (_adminAccess.IsUnlocked)
            return true;

        if (_adminAccess.HasCredentialError)
        {
            MessageBox.Show(
                "Das Admin-Zugriffsprofil ist beschädigt. Der normale Produktionsbetrieb bleibt verfügbar, " +
                "administrative Funktionen bleiben jedoch gesperrt.\n\n" +
                $"Datei: {_adminAccess.CredentialPath}\nFehler: {_adminAccess.CredentialError}",
                "Administration gesperrt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        if (!_adminAccess.IsConfigured)
        {
            var setup = new AdminPasswordDialog(AdminPasswordDialogMode.Setup) { Owner = this };
            if (setup.ShowDialog() != true)
                return false;

            try
            {
                _adminAccess.SetPassword(setup.Password);
                MessageBox.Show(
                    "Das Admin-Passwort wurde eingerichtet. Die administrativen Bereiche sind jetzt entsperrt.",
                    "Partcounter Administration",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Admin-Passwort", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        var unlock = new AdminPasswordDialog(AdminPasswordDialogMode.Unlock) { Owner = this };
        if (unlock.ShowDialog() != true)
            return false;

        if (_adminAccess.TryUnlock(unlock.Password))
            return true;

        MessageBox.Show(
            "Das eingegebene Admin-Passwort ist nicht korrekt.",
            "Administration gesperrt",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        return false;
    }

    private void AttachAdminControls()
    {
        if (_adminButton is not null)
            return;

        _operatingModeButton = FindBoundButton(this, "OperatingModeButtonText");
        if (_operatingModeButton is null)
            return;

        BindingOperations.ClearBinding(_operatingModeButton, Button.CommandProperty);
        _operatingModeButton.Click += OnOperatingModeButtonClick;
        _operatingModeButton.ToolTip = "Simulation/Echtbetrieb ist eine geschützte Systemeinstellung.";

        if (_operatingModeButton.Parent is not Panel parent)
            return;

        _adminButton = new Button
        {
            MinWidth = 126,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(10, 5, 10, 5)
        };
        _adminButton.Click += OnAdminButtonClick;

        var menu = new ContextMenu();
        var changePassword = new MenuItem { Header = "Admin-Passwort ändern" };
        changePassword.Click += (_, _) => ChangeAdminPassword();
        var lockNow = new MenuItem { Header = "Administration sperren" };
        lockNow.Click += (_, _) => LockAdministration();
        menu.Items.Add(changePassword);
        menu.Items.Add(lockNow);
        _adminButton.ContextMenu = menu;

        parent.Children.Add(_adminButton);
    }

    private void OnOperatingModeButtonClick(object sender, RoutedEventArgs e)
    {
        if (!EnsureAdminUnlocked())
            return;

        var command = _viewModel.ToggleOperatingModeCommand;
        if (command.CanExecute(null))
            command.Execute(null);
    }

    private void OnAdminButtonClick(object sender, RoutedEventArgs e)
    {
        if (_adminAccess.IsUnlocked)
            LockAdministration();
        else
            EnsureAdminUnlocked();
    }

    private void ChangeAdminPassword()
    {
        if (!EnsureAdminUnlocked())
            return;

        var dialog = new AdminPasswordDialog(AdminPasswordDialogMode.Change) { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            _adminAccess.SetPassword(dialog.Password);
            MessageBox.Show(
                "Das Admin-Passwort wurde geändert.",
                "Partcounter Administration",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Admin-Passwort", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LockAdministration()
    {
        _adminAccess.Lock();

        if (MainTabs.SelectedItem is TabItem selected && _protectedTabs.ContainsKey(selected))
        {
            _tabGuardBusy = true;
            try
            {
                var fallback = MainTabs.Items.OfType<TabItem>().FirstOrDefault(t => !_protectedTabs.ContainsKey(t));
                if (fallback is not null)
                {
                    MainTabs.SelectedItem = fallback;
                    _lastAllowedTab = fallback;
                }
            }
            finally
            {
                _tabGuardBusy = false;
            }
        }
    }

    private void OnAdminAccessStateChanged(object? sender, EventArgs e) =>
        _ = Dispatcher.BeginInvoke(new Action(RefreshAdminUi));

    private void RefreshAdminUi()
    {
        foreach (var pair in _protectedTabs)
            pair.Key.Header = _adminAccess.IsUnlocked ? $"🔓 {pair.Value}" : $"🔒 {pair.Value}";

        if (_adminButton is null)
            return;

        _adminButton.Content = _adminAccess.IsUnlocked ? "Admin sperren" : "Admin entsperren";
        _adminButton.ToolTip = _adminAccess.IsUnlocked
            ? "Administrative Funktionen sind freigegeben. Klicken zum Sperren. Rechtsklick: Passwort ändern."
            : "Administrative Funktionen entsperren. Der normale Produktionsbetrieb ist ohne Anmeldung verfügbar.";
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
            ? AppVersionInfo.SimulationStatus
            : AppVersionInfo.ProductionStatus;
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

    private static Button? FindBoundButton(DependencyObject root, string contentBindingPath)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Button button)
            {
                var expression = BindingOperations.GetBindingExpression(button, ContentControl.ContentProperty);
                if (expression?.ParentBinding.Path?.Path == contentBindingPath)
                    return button;
            }

            var nested = FindBoundButton(child, contentBindingPath);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private void OnVisibleMachinesChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        ScheduleMachineContextMenuAttachment();

    private void OnMachineContainerStatusChanged(object? sender, EventArgs e)
    {
        if (MachineItemsControl.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            ScheduleMachineContextMenuAttachment();
    }

    private void ScheduleMachineContextMenuAttachment()
    {
        if (_machineContextMenuAttachPending)
            return;

        _machineContextMenuAttachPending = true;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            try
            {
                AttachMachineContextMenus();
            }
            finally
            {
                _machineContextMenuAttachPending = false;
            }
        }));
    }

    private void AttachMachineContextMenus()
    {
        foreach (var machine in _viewModel.VisibleMachines)
        {
            if (MachineItemsControl.ItemContainerGenerator.ContainerFromItem(machine) is not FrameworkElement container)
                continue;

            if (container.ContextMenu?.Tag is MachineState currentMachine && ReferenceEquals(currentMachine, machine))
                continue;

            var menu = CreateMachineContextMenu(machine);
            menu.Tag = machine;
            container.ContextMenu = menu;
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

        _ = Dispatcher.BeginInvoke(
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
            _ = Dispatcher.BeginInvoke(
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
        _adminAccess.StateChanged -= OnAdminAccessStateChanged;
        MainTabs.SelectionChanged -= OnMainTabSelectionChanged;
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
