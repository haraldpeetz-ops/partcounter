using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Partcounter;

public partial class MainWindow
{
    private TabItem? _administrationTab;
    private TabControl? _administrationTabs;
    private DispatcherTimer? _adminHubInitializationTimer;
    private TextBlock? _adminModeStatusText;
    private TextBlock? _adminSessionStatusText;
    private Button? _adminModeToggleButton;
    private bool _adminHubScheduled;

    internal void ScheduleAdminHubInitialization()
    {
        if (_adminHubScheduled)
            return;

        _adminHubScheduled = true;
        _viewModel.PropertyChanged += OnAdminHubViewModelPropertyChanged;
        _adminAccess.StateChanged += OnAdminHubAccessStateChanged;
        Closed += OnAdminHubClosed;

        _adminHubInitializationTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(180),
            DispatcherPriority.ApplicationIdle,
            (_, _) =>
            {
                if (!TryBuildAdministrationHub() || _adminHubInitializationTimer is null)
                    return;

                _adminHubInitializationTimer.Stop();
                _adminHubInitializationTimer = null;
            },
            Dispatcher);
        _adminHubInitializationTimer.Start();
    }

    private bool TryBuildAdministrationHub()
    {
        if (_administrationTab is not null)
            return true;

        if (_machineSetupTab is null ||
            _labelDesignerTab is null ||
            _commissioningTab is null ||
            _commissioningFleetTab is null ||
            _alsTab is null ||
            _adminButton is null)
        {
            return false;
        }

        var settingsTab = MainTabs.Items
            .OfType<TabItem>()
            .FirstOrDefault(tab => HeaderContains(tab, "Einstellungen / Druck"));
        if (settingsTab is null)
            return false;

        var settingsStack = settingsTab.Content switch
        {
            ScrollViewer { Content: StackPanel stack } => stack,
            StackPanel stack => stack,
            _ => null
        };
        if (settingsStack is null)
            return false;

        // Erst umgruppieren, wenn die vorhandenen Einstellungs-Erweiterungen vollständig montiert sind.
        // Dadurch bleiben Branding, Updatecenter und Produktionsbereitschaft unverändert funktionsfähig.
        var hasBranding = settingsStack.Children.OfType<FrameworkElement>()
            .Any(element => Equals(element.Tag, "PartcounterCompanyBrandingSettings"));
        var hasUpdateCenter = settingsStack.Children.OfType<FrameworkElement>()
            .Any(element => Equals(element.Tag, "PartcounterUpdateCenter") || Equals(element.Tag, "PartcounterR00114UpdateCenter"));
        var hasProductionReadiness = settingsStack.Children.OfType<FrameworkElement>()
            .Any(element => Equals(element.Tag, "PartcounterProductionReadiness") || Equals(element.Tag, "PartcounterR00115ProductionReadiness"));

        if (!hasBranding || !hasUpdateCenter || !hasProductionReadiness)
            return false;

        var adminTabs = new TabControl
        {
            Margin = new Thickness(8),
            TabStripPlacement = Dock.Top
        };
        _administrationTabs = adminTabs;
        adminTabs.SelectionChanged += OnAdministrationTabSelectionChanged;

        adminTabs.Items.Add(new TabItem
        {
            Header = "Betriebsart",
            Content = BuildOperatingModeAdministrationView()
        });
        adminTabs.Items.Add(new TabItem
        {
            Header = "Zugriff / Sicherheit",
            Content = BuildAdminAccessView()
        });

        MoveToAdministration(adminTabs, _machineSetupTab, "Maschinen / Modbus");
        MoveToAdministration(adminTabs, _labelDesignerTab, "Etiketteneditor");
        MoveToAdministration(adminTabs, _commissioningTab, "Inbetriebnahme / Diagnose");
        MoveToAdministration(adminTabs, _commissioningFleetTab, "Rolloutstatus 30 Maschinen");
        MoveToAdministration(adminTabs, _alsTab, "ARBURG ALS");
        MoveToAdministration(adminTabs, settingsTab, "Einstellungen / Druck");

        _administrationTab = new TabItem
        {
            Header = "Administration",
            Content = BuildAdministrationRoot(adminTabs)
        };
        MainTabs.Items.Add(_administrationTab);
        _protectedTabs[_administrationTab] = "Administration";

        // Unsichtbare Kompatibilitätsreiter erhalten die bisherigen Headernamen. Bestehende
        // Hilfe-/Screenshot-Navigation kann sie weiterhin ansteuern; nach Auswahl wird auf den
        // entsprechenden Unterreiter im Admin-Hub umgeleitet. In der normalen UI sind sie unsichtbar.
        AddAdminNavigationAlias("Maschinen / Modbus");
        AddAdminNavigationAlias("Etiketteneditor");
        AddAdminNavigationAlias("Inbetriebnahme / Diagnose");
        AddAdminNavigationAlias("Rolloutstatus 30 Maschinen");
        AddAdminNavigationAlias("ARBURG ALS");
        AddAdminNavigationAlias("Einstellungen / Druck");

        _operatingModeButton ??= FindBoundButton(this, "OperatingModeButtonText");
        if (_operatingModeButton is not null)
        {
            _operatingModeButton.Visibility = Visibility.Collapsed;
            _operatingModeButton.IsEnabled = false;
            _operatingModeButton.IsTabStop = false;
            _operatingModeButton.ToolTip = "Die Betriebsart kann ausschließlich unter Administration → Betriebsart geändert werden.";
        }

        RefreshAdminUi();
        RefreshAdminHubState();
        return true;
    }

    private UIElement BuildAdministrationRoot(TabControl adminTabs)
    {
        var root = new DockPanel();
        var information = new Border
        {
            DockPanel.Dock = Dock.Top,
            Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xF3, 0xF7)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xC6, 0xD1, 0xDC)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(8, 8, 8, 0)
        };
        var stack = new StackPanel();
        information.Child = stack;
        stack.Children.Add(new TextBlock
        {
            Text = "Geschützter Administrationsbereich",
            FontSize = 18,
            FontWeight = FontWeights.Bold
        });
        _adminSessionStatusText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(0x42, 0x54, 0x66))
        };
        stack.Children.Add(_adminSessionStatusText);
        stack.Children.Add(new TextBlock
        {
            Text = "Alle systemverändernden Funktionen sind hier zusammengefasst. Leitstand, Artikelstamm und VE-Historie bleiben für den normalen Produktionsanwender frei zugänglich.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x71, 0x80))
        });
        root.Children.Add(information);
        root.Children.Add(adminTabs);
        return root;
    }

    private UIElement BuildOperatingModeAdministrationView()
    {
        var root = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var stack = new StackPanel
        {
            Margin = new Thickness(22),
            MaxWidth = 900,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        root.Content = stack;

        stack.Children.Add(new TextBlock
        {
            Text = "Betriebsart",
            FontSize = 24,
            FontWeight = FontWeights.Bold
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Das Umschalten zwischen Simulation und realer Modbus-Kommunikation ist eine administrative Systemfunktion. Produktionsanwender können die aktuelle Betriebsart sehen, aber nicht verändern.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 14),
            Foreground = new SolidColorBrush(Color.FromRgb(0x56, 0x64, 0x73))
        });

        var modeBox = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xDE, 0xE6)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16)
        };
        var modeStack = new StackPanel();
        modeBox.Child = modeStack;
        modeStack.Children.Add(new TextBlock { Text = "Aktuelle Betriebsart", FontWeight = FontWeights.SemiBold });
        _adminModeStatusText = new TextBlock
        {
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 4, 0, 10)
        };
        modeStack.Children.Add(_adminModeStatusText);

        _adminModeToggleButton = new Button
        {
            Padding = new Thickness(16, 9, 16, 9),
            HorizontalAlignment = HorizontalAlignment.Left,
            FontWeight = FontWeights.SemiBold
        };
        _adminModeToggleButton.Click += (_, _) => ToggleOperatingModeFromAdminHub();
        modeStack.Children.Add(_adminModeToggleButton);
        stack.Children.Add(modeBox);

        stack.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xF4, 0xD6)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD6, 0xA5, 0x00)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 14, 0, 0),
            Child = new TextBlock
            {
                Text = "Achtung: Im Echtbetrieb kommuniziert Partcounter mit den freigegebenen Siemens-LOGO!-Stationen. Ein Wechsel der Betriebsart darf nur bewusst durch autorisiertes Personal erfolgen.",
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.SemiBold
            }
        });

        return root;
    }

    private UIElement BuildAdminAccessView()
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(22),
            MaxWidth = 900,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        stack.Children.Add(new TextBlock
        {
            Text = "Zugriff / Sicherheit",
            FontSize = 24,
            FontWeight = FontWeights.Bold
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Die Entsperrung gilt nur für die laufende Partcounter-Sitzung. Nach dem Sperren sind alle Administrationsunterreiter wieder geschützt.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 14),
            Foreground = new SolidColorBrush(Color.FromRgb(0x56, 0x64, 0x73))
        });

        var buttons = new WrapPanel();
        var lockButton = new Button
        {
            Content = "Administration jetzt sperren",
            Padding = new Thickness(14, 8, 14, 8)
        };
        lockButton.Click += (_, _) => LockAdministration();
        var passwordButton = new Button
        {
            Content = "Admin-Passwort ändern",
            Padding = new Thickness(14, 8, 14, 8)
        };
        passwordButton.Click += (_, _) => ChangeAdminPassword();
        buttons.Children.Add(lockButton);
        buttons.Children.Add(passwordButton);
        stack.Children.Add(buttons);
        return stack;
    }

    private void MoveToAdministration(TabControl target, TabItem tab, string displayName)
    {
        MainTabs.Items.Remove(tab);
        _protectedTabs.Remove(tab);
        tab.Header = displayName;
        target.Items.Add(tab);
    }

    private void AddAdminNavigationAlias(string displayName)
    {
        var alias = new TabItem
        {
            Header = displayName,
            Visibility = Visibility.Collapsed,
            Tag = $"PartcounterAdminAlias:{displayName}"
        };
        MainTabs.Items.Add(alias);
        _protectedTabs[alias] = displayName;
    }

    private void OnAdministrationTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, _administrationTabs))
            return;

        RefreshAdminHubState();
    }

    private void OnMainAdminAliasSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Reserved for compatibility; MainTabs is handled through the window's existing selection event.
    }

    private void NavigateAdminAliasIfNeeded()
    {
        if (MainTabs.SelectedItem is not TabItem selected ||
            selected.Tag is not string tag ||
            !tag.StartsWith("PartcounterAdminAlias:", StringComparison.Ordinal))
        {
            return;
        }

        var displayName = tag["PartcounterAdminAlias:".Length..];
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() => OpenAdministrationSection(displayName)));
    }

    private void OpenAdministrationSection(string displayName)
    {
        if (_administrationTab is null || _administrationTabs is null || !_adminAccess.IsUnlocked)
            return;

        var target = _administrationTabs.Items
            .OfType<TabItem>()
            .FirstOrDefault(tab => HeaderContains(tab, displayName));
        if (target is null)
            return;

        _administrationTabs.SelectedItem = target;
        MainTabs.SelectedItem = _administrationTab;
        _lastAllowedTab = _administrationTab;
        RefreshAdminHubState();
    }

    private void ToggleOperatingModeFromAdminHub()
    {
        if (!EnsureAdminUnlocked())
            return;

        var switchingToProduction = _viewModel.IsSimulationMode;
        var message = switchingToProduction
            ? "Echtbetrieb aktivieren?\n\nPartcounter wird anschließend mit den freigegebenen LOGO!-Stationen über Modbus TCP kommunizieren."
            : "Simulation aktivieren?\n\nDie reale Modbus-Kommunikation wird beendet und Partcounter arbeitet anschließend ohne Steuerbefehle an die LOGO!-Stationen.";
        var answer = MessageBox.Show(
            message,
            "Partcounter – Betriebsart ändern",
            MessageBoxButton.YesNo,
            switchingToProduction ? MessageBoxImage.Warning : MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
            return;

        var command = _viewModel.ToggleOperatingModeCommand;
        if (command.CanExecute(null))
            command.Execute(null);
    }

    private void OnAdminHubViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ViewModels.MainViewModel.IsSimulationMode) or nameof(ViewModels.MainViewModel.SystemStatusText))
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(RefreshAdminHubState));
    }

    private void OnAdminHubAccessStateChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(RefreshAdminHubState));

    private void RefreshAdminHubState()
    {
        if (_administrationTab is not null)
        {
            var currentSection = (_administrationTabs?.SelectedItem as TabItem)?.Header?.ToString();
            _administrationTab.Header = _adminAccess.IsUnlocked
                ? string.IsNullOrWhiteSpace(currentSection)
                    ? "🔓 Administration"
                    : $"🔓 Administration · {currentSection}"
                : "🔒 Administration";
        }

        if (_adminSessionStatusText is not null)
        {
            _adminSessionStatusText.Text = _adminAccess.IsUnlocked
                ? "🔓 Administration ist für diese Sitzung entsperrt."
                : "🔒 Administration ist gesperrt.";
        }

        if (_adminModeStatusText is not null)
        {
            _adminModeStatusText.Text = _viewModel.IsSimulationMode
                ? Services.AppVersionInfo.SimulationStatus
                : Services.AppVersionInfo.ProductionStatus;
            _adminModeStatusText.Foreground = _viewModel.IsSimulationMode
                ? new SolidColorBrush(Color.FromRgb(0x8A, 0x5A, 0x00))
                : new SolidColorBrush(Color.FromRgb(0x2D, 0x6A, 0x35));
        }

        if (_adminModeToggleButton is not null)
        {
            _adminModeToggleButton.Content = _viewModel.IsSimulationMode
                ? "Echtbetrieb aktivieren"
                : "Simulation aktivieren";
            _adminModeToggleButton.IsEnabled = _adminAccess.IsUnlocked;
        }

        NavigateAdminAliasIfNeeded();
    }

    private static bool HeaderContains(TabItem tab, string value) =>
        tab.Header?.ToString()?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;

    private void OnAdminHubClosed(object? sender, EventArgs e)
    {
        if (_adminHubInitializationTimer is not null)
        {
            _adminHubInitializationTimer.Stop();
            _adminHubInitializationTimer = null;
        }

        if (_administrationTabs is not null)
            _administrationTabs.SelectionChanged -= OnAdministrationTabSelectionChanged;
        _viewModel.PropertyChanged -= OnAdminHubViewModelPropertyChanged;
        _adminAccess.StateChanged -= OnAdminHubAccessStateChanged;
        Closed -= OnAdminHubClosed;
    }
}
