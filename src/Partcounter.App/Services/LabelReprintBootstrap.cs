using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Partcounter.Models;
using Partcounter.ViewModels;
using Partcounter.Views;

namespace Partcounter.Services;

public sealed class LabelReprintBootstrap
{
    private static readonly Dictionary<MainWindow, LabelReprintBootstrap> Instances = new();

    private readonly MainWindow _window;
    private readonly LabelReprintService _service = new();
    private MainViewModel? _main;
    private DataGrid? _historyGrid;
    private PackagingUnitRecord? _selectedRecord;
    private TextBlock? _selectionText;
    private TextBlock? _statusText;
    private TextBlock? _journalText;
    private Button? _reprintButton;
    private Button? _journalButton;
    private INotifyPropertyChanged? _mainNotifier;

    private LabelReprintBootstrap(MainWindow window) => _window = window;

    public static void Attach(MainWindow window)
    {
        if (Instances.ContainsKey(window))
            return;

        var instance = new LabelReprintBootstrap(window);
        Instances[window] = instance;
        window.Loaded += instance.OnLoaded;
        window.Closed += instance.OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_window.DataContext is not MainViewModel main)
                return;

            _main = main;
            _mainNotifier = main;
            _mainNotifier.PropertyChanged += OnMainPropertyChanged;
            await _service.InitializeAsync();

            _window.Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(() =>
                {
                    AttachHistoryReprintPanel();
                    UpdateRevisionUi();
                }));
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Die Etiketten-Nachdruck-/Snapshotfunktion konnte nicht initialisiert werden.\n\n{ex.Message}",
                "Partcounter R001.18",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void AttachHistoryReprintPanel()
    {
        var mainTabs = FindDescendant<TabControl>(_window, tabs =>
            tabs.Items.OfType<TabItem>().Any(tab => string.Equals(tab.Header?.ToString(), "Leitstand", StringComparison.Ordinal)));
        var historyTab = mainTabs?.Items.OfType<TabItem>().FirstOrDefault(tab =>
            string.Equals(tab.Header?.ToString(), "VE-Historie", StringComparison.Ordinal));
        if (historyTab is null)
            return;

        if (historyTab.Content is Grid existingGrid &&
            (Equals(existingGrid.Tag, "PartcounterR00118LabelReprint") || Equals(existingGrid.Tag, "PartcounterR00117LabelReprint")))
            return;
        if (historyTab.Content is not DataGrid dataGrid)
            return;

        _historyGrid = dataGrid;
        historyTab.Content = null;

        var root = new Grid { Tag = "PartcounterR00118LabelReprint", Margin = new Thickness(10) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var panel = BuildToolbar();
        root.Children.Add(panel);

        dataGrid.Margin = new Thickness(0);
        dataGrid.SelectionMode = DataGridSelectionMode.Single;
        dataGrid.SelectionUnit = DataGridSelectionUnit.FullRow;
        dataGrid.SelectionChanged += OnHistorySelectionChanged;
        Grid.SetRow(dataGrid, 1);
        root.Children.Add(dataGrid);
        AttachContextMenu(dataGrid);

        historyTab.Content = root;
        UpdateSelectionUi();
    }

    private Border BuildToolbar()
    {
        _selectionText = new TextBlock
        {
            Text = "Bitte eine bereits gedruckte Verpackungseinheit auswählen.",
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeights.SemiBold
        };
        _journalText = new TextBlock
        {
            Text = "Nachdrucke: –",
            Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x71, 0x80)),
            Margin = new Thickness(0, 3, 0, 0)
        };
        _statusText = new TextBlock
        {
            Text = "R001.18 archiviert beim regulären VE-Druck das verwendete Etikettenlayout als unveränderlichen Snapshot mit SHA-256-Prüfung.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x56, 0x64, 0x73)),
            FontSize = 11,
            Margin = new Thickness(0, 4, 0, 0)
        };

        _reprintButton = new Button
        {
            Content = "Etikett nachdrucken…",
            Padding = new Thickness(13, 7, 13, 7),
            MinWidth = 155,
            IsEnabled = false,
            ToolTip = "Ausgewähltes bereits gedrucktes VE-Etikett mit historischen VE-Daten und – falls vorhanden – Original-Layout-Snapshot erneut drucken"
        };
        _reprintButton.Click += async (_, _) => await ReprintSelectedAsync();

        _journalButton = new Button
        {
            Content = "Druckjournal anzeigen",
            Padding = new Thickness(13, 7, 13, 7),
            MinWidth = 145,
            IsEnabled = false,
            ToolTip = "Protokollierte Nachdruckversuche einschließlich verwendeter Layoutquelle anzeigen"
        };
        _journalButton.Click += async (_, _) => await ShowJournalAsync();

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var heading = new StackPanel();
        heading.Children.Add(new TextBlock
        {
            Text = "Etiketten-Nachdruck",
            FontSize = 18,
            FontWeight = FontWeights.Bold
        });
        heading.Children.Add(new TextBlock
        {
            Text = "R001.18 · Reprint · Layout-Snapshot · Druckjournal",
            Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x71, 0x80)),
            FontSize = 11
        });
        grid.Children.Add(heading);

        var selection = new StackPanel { Margin = new Thickness(12, 0, 12, 0) };
        selection.Children.Add(_selectionText);
        selection.Children.Add(_journalText);
        selection.Children.Add(_statusText);
        Grid.SetColumn(selection, 1);
        grid.Children.Add(selection);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        buttons.Children.Add(_reprintButton);
        buttons.Children.Add(_journalButton);
        Grid.SetColumn(buttons, 2);
        grid.Children.Add(buttons);

        return new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xDE, 0xE6)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8),
            Child = grid
        };
    }

    private void AttachContextMenu(DataGrid dataGrid)
    {
        var menu = dataGrid.ContextMenu ?? new ContextMenu();
        if (menu.Items.OfType<MenuItem>().Any(i => Equals(i.Tag, "PartcounterR00117Reprint") || Equals(i.Tag, "PartcounterR00118Reprint")))
            return;

        if (menu.Items.Count > 0)
            menu.Items.Add(new Separator());

        var reprint = new MenuItem
        {
            Header = "Etikett nachdrucken…",
            Tag = "PartcounterR00118Reprint"
        };
        reprint.Click += async (_, _) => await ReprintSelectedAsync();
        var journal = new MenuItem
        {
            Header = "Nachdruckjournal anzeigen",
            Tag = "PartcounterR00118Journal"
        };
        journal.Click += async (_, _) => await ShowJournalAsync();
        menu.Items.Add(reprint);
        menu.Items.Add(journal);
        dataGrid.ContextMenu = menu;
    }

    private async void OnHistorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedRecord = _historyGrid?.SelectedItem as PackagingUnitRecord;
        await UpdateSelectionUiAsync();
    }

    private void UpdateSelectionUi()
    {
        _ = UpdateSelectionUiAsync();
    }

    private async Task UpdateSelectionUiAsync()
    {
        var record = _selectedRecord;
        if (record is null)
        {
            if (_selectionText is not null)
                _selectionText.Text = "Bitte eine bereits gedruckte Verpackungseinheit auswählen.";
            if (_journalText is not null)
                _journalText.Text = "Nachdrucke: –";
            SetButtons(false, false);
            return;
        }

        var printed = IsMarkedPrinted(record);
        var reprintCount = await _service.GetSuccessfulReprintCountAsync(record.Id);
        LabelPrintSnapshot? snapshot = null;
        try
        {
            snapshot = await _service.LoadPrintSnapshotAsync(record.Id);
        }
        catch (Exception ex)
        {
            if (_statusText is not null)
                _statusText.Text = $"WARNUNG: Vorhandener Layout-Snapshot kann nicht verifiziert werden: {ex.Message}";
        }

        if (_selectionText is not null)
        {
            _selectionText.Text =
                $"M{record.MachineNumber:00} · VE {record.VeNumber} · Auftrag {record.OrderNumber} · Artikel {record.ArticleNumber} · {record.ActualQuantity:N0} Teile · Etikett: {record.LabelStatus}";
        }
        if (_journalText is not null)
        {
            var snapshotText = snapshot is null
                ? "Layout-Snapshot: nicht vorhanden"
                : $"Layout-Snapshot: {snapshot.TemplateName} · SHA256 {snapshot.ShortHash}…";
            _journalText.Text = $"Erfolgreiche Nachdrucke: {reprintCount:N0} · Originaldruck: {(record.PrintedAtUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") ?? "nicht protokolliert")} · {snapshotText}";
        }
        if (_statusText is not null)
        {
            if (!printed)
            {
                _statusText.Text = "Dieser Datensatz ist nicht als bereits gedruckt markiert. Die Reprint-Funktion ist deshalb gesperrt.";
            }
            else if (snapshot is not null)
            {
                _statusText.Text = $"Original-Layout-Snapshot vorhanden: {snapshot.DisplayText}. Nachdruck verwendet exakt diese archivierte Vorlagendefinition und die historischen VE-Daten.";
            }
            else
            {
                _statusText.Text = "Ältere VE ohne Layout-Snapshot: Der Nachdruck verwendet die historischen VE-Daten, aber das aktuell aufgelöste Etikettenlayout. Diese Fallback-Verwendung wird im Druckjournal protokolliert.";
            }
        }

        SetButtons(printed, true);
    }

    private void SetButtons(bool reprintEnabled, bool journalEnabled)
    {
        if (_reprintButton is not null)
            _reprintButton.IsEnabled = reprintEnabled;
        if (_journalButton is not null)
            _journalButton.IsEnabled = journalEnabled;
    }

    private async Task ReprintSelectedAsync()
    {
        var record = _selectedRecord ?? _historyGrid?.SelectedItem as PackagingUnitRecord;
        if (record is null)
        {
            MessageBox.Show("Bitte zuerst eine VE in der Historie auswählen.", "Etikett nachdrucken", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!IsMarkedPrinted(record))
        {
            MessageBox.Show(
                "Die ausgewählte VE ist nicht als bereits gedruckt gekennzeichnet. Ein Nachdruck wird deshalb nicht ausgeführt.",
                "Etikett nachdrucken",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var reprintCount = await _service.GetSuccessfulReprintCountAsync(record.Id);
        var dialog = new LabelReprintDialog(record, reprintCount) { Owner = _window };
        if (dialog.ShowDialog() != true)
            return;

        if (_statusText is not null)
            _statusText.Text = $"Etikett VE {record.VeNumber} wird an '{_main?.LabelPrinterName}' übergeben …";

        try
        {
            var result = await _service.ReprintAsync(record, _main?.LabelPrinterName ?? string.Empty, dialog.ReprintReason);
            if (result.Successful)
            {
                if (_statusText is not null)
                    _statusText.Text = $"Nachdruck #{result.ReprintNumber} erfolgreich · {result.LayoutSource}";
                MessageBox.Show(
                    $"Etikett für M{record.MachineNumber:00} / VE {record.VeNumber} wurde als Nachdruck #{result.ReprintNumber} an den Drucker übergeben.\n\nVE-ID: {record.Id}\nGrund: {result.Reason}\nLayout: {result.LayoutSource}",
                    result.HistoricalSnapshotUsed ? "Etikett aus Original-Snapshot nachgedruckt" : "Etikett nachgedruckt – Layout-Fallback",
                    MessageBoxButton.OK,
                    result.HistoricalSnapshotUsed ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            else
            {
                if (_statusText is not null)
                    _statusText.Text = $"Nachdruck #{result.ReprintNumber} fehlgeschlagen: {result.ErrorMessage} · {result.LayoutSource}";
                MessageBox.Show(
                    $"{result.ErrorMessage}\n\nLayout: {result.LayoutSource}",
                    "Etikett-Nachdruck fehlgeschlagen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            await UpdateSelectionUiAsync();
        }
        catch (Exception ex)
        {
            if (_statusText is not null)
                _statusText.Text = $"Nachdruck fehlgeschlagen: {ex.Message}";
            MessageBox.Show(ex.Message, "Etikett-Nachdruck", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ShowJournalAsync()
    {
        var record = _selectedRecord ?? _historyGrid?.SelectedItem as PackagingUnitRecord;
        if (record is null)
        {
            MessageBox.Show("Bitte zuerst eine VE auswählen.", "Druckjournal", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var entries = await _service.LoadJournalAsync(record.Id, 100);
            var window = new LabelReprintJournalWindow(record, entries) { Owner = _window };
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Druckjournal", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static bool IsMarkedPrinted(PackagingUnitRecord record) =>
        record.PrintedAtUtc.HasValue || string.Equals(record.LabelStatus, "Printed", StringComparison.OrdinalIgnoreCase);

    private void OnMainPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsSimulationMode) or nameof(MainViewModel.SystemStatusText))
        {
            _window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(UpdateRevisionUi));
        }
        else if (e.PropertyName == nameof(MainViewModel.LabelPrinterName) && _selectedRecord is not null)
        {
            _window.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(UpdateSelectionUi));
        }
    }

    private void UpdateRevisionUi()
    {
        _window.Title = "Partcounter R001.18";

        foreach (var text in FindDescendants<TextBlock>(_window))
        {
            var expression = BindingOperations.GetBindingExpression(text, TextBlock.TextProperty);
            if (expression?.ParentBinding.Path?.Path == "SystemStatusText" ||
                text.Text?.StartsWith("R001.17 · SIMULATION", StringComparison.Ordinal) == true ||
                text.Text?.StartsWith("R001.17 · ECHTBETRIEB", StringComparison.Ordinal) == true ||
                text.Text?.StartsWith("R001.16 · SIMULATION", StringComparison.Ordinal) == true ||
                text.Text?.StartsWith("R001.16 · ECHTBETRIEB", StringComparison.Ordinal) == true)
            {
                BindingOperations.ClearBinding(text, TextBlock.TextProperty);
                var simulation = _main?.IsSimulationMode ?? true;
                text.Text = simulation
                    ? "R001.18 · SIMULATION"
                    : "R001.18 · ECHTBETRIEB MODBUS TCP";
                continue;
            }

            if (text.Text?.StartsWith("Installiert: R001.17 /", StringComparison.Ordinal) == true)
                text.Text = text.Text.Replace("Installiert: R001.17 /", "Installiert: R001.18 /", StringComparison.Ordinal);
            else if (text.Text?.StartsWith("Installiert: R001.16 /", StringComparison.Ordinal) == true)
                text.Text = text.Text.Replace("Installiert: R001.16 /", "Installiert: R001.18 /", StringComparison.Ordinal);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_mainNotifier is not null)
            _mainNotifier.PropertyChanged -= OnMainPropertyChanged;
        if (_historyGrid is not null)
            _historyGrid.SelectionChanged -= OnHistorySelectionChanged;
        _window.Loaded -= OnLoaded;
        _window.Closed -= OnClosed;
        Instances.Remove(_window);
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                yield return match;

            foreach (var nested in FindDescendants<T>(child))
                yield return nested;
        }
    }

    private static T? FindDescendant<T>(DependencyObject root, Predicate<T> predicate) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match && predicate(match))
                return match;

            var nested = FindDescendant(child, predicate);
            if (nested is not null)
                return nested;
        }

        return null;
    }
}
