using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Partcounter.Services;

namespace Partcounter.Views;

public sealed class HelpCenterWindow : Window
{
    private readonly PartcounterHelpService _help = new();
    private readonly ObservableCollection<HelpTopic> _visibleTopics = new();
    private readonly TextBox _searchBox = new();
    private readonly ComboBox _categoryBox = new();
    private readonly ListBox _topicList = new();
    private readonly TextBlock _title = new();
    private readonly TextBlock _category = new();
    private readonly FlowDocumentScrollViewer _body = new();
    private readonly WrapPanel _dependsPanel = new();
    private readonly WrapPanel _usedByPanel = new();
    private readonly Border _screenshotBorder = new();
    private readonly Image _screenshotImage = new();
    private readonly TextBlock _screenshotTitle = new();
    private readonly TextBlock _screenshotCaption = new();
    private readonly TextBlock _screenshotPlaceholder = new();
    private readonly Button _copyScreenshotInstructionButton = new();
    private readonly Button _autoCaptureButton = new();
    private readonly Button _openCaptureFolderButton = new();
    private readonly TextBlock _captureStatus = new();
    private HelpTopic? _currentTopic;

    public HelpCenterWindow()
    {
        Title = "Partcounter R001.20 – Hilfe & Dokumentation";
        Width = 1480;
        Height = 920;
        MinWidth = 1050;
        MinHeight = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0xF2, 0xF4, 0xF7));

        Content = BuildUi();
        _searchBox.TextChanged += (_, _) => RefreshFilter();
        _categoryBox.SelectionChanged += (_, _) => RefreshFilter();
        _topicList.SelectionChanged += (_, _) => ShowTopic(_topicList.SelectedItem as HelpTopic);
        _copyScreenshotInstructionButton.Click += (_, _) => CopyScreenshotInstruction();
        _autoCaptureButton.Click += async (_, _) => await RunAutomaticCaptureAsync();
        _openCaptureFolderButton.Click += (_, _) => OpenCaptureFolder();
        PreviewKeyDown += OnPreviewKeyDown;

        foreach (var category in _help.Categories)
            _categoryBox.Items.Add(category);
        _categoryBox.SelectedIndex = 0;
        RefreshFilter();
    }

    public void OpenTopic(string id)
    {
        var topic = _help.Find(id);
        if (topic is null) return;
        _categoryBox.SelectedItem = "Alle";
        _searchBox.Clear();
        RefreshFilter();
        _topicList.SelectedItem = _visibleTopics.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
        if (_topicList.SelectedItem is not null)
            _topicList.ScrollIntoView(_topicList.SelectedItem);
    }

    private UIElement BuildUi()
    {
        var root = new Grid { Margin = new Thickness(12) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xDE, 0xE6)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12)
        };
        Grid.SetColumn(left, 0);
        root.Children.Add(left);

        var leftDock = new DockPanel();
        left.Child = leftDock;
        var filters = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        DockPanel.SetDock(filters, Dock.Top);
        filters.Children.Add(new TextBlock { Text = "Partcounter Hilfe", FontSize = 22, FontWeight = FontWeights.Bold });
        filters.Children.Add(new TextBlock
        {
            Text = "R001.20 · Bedienung · Inbetriebnahme · Diagnose · Auto-Screenshots",
            Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x71, 0x80)),
            Margin = new Thickness(0, 3, 0, 8),
            TextWrapping = TextWrapping.Wrap
        });
        filters.Children.Add(new TextBlock
        {
            Text = "Funktion, Begriff oder Fehlertext suchen",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 3)
        });
        _searchBox.MinHeight = 32;
        _searchBox.ToolTip = "Suche in Titel, Beschreibung, Schlagwörtern, Screenshot-Hinweisen und Abhängigkeiten";
        filters.Children.Add(_searchBox);
        filters.Children.Add(new TextBlock { Text = "Kategorie", Margin = new Thickness(0, 8, 0, 3), FontWeight = FontWeights.SemiBold });
        _categoryBox.MinHeight = 30;
        filters.Children.Add(_categoryBox);
        filters.Children.Add(new TextBlock
        {
            Text = "F1 öffnet direkt die Hilfe zum aktuell gewählten Partcounter-Bereich. Strg+F springt hier in die Suche.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x71, 0x80)),
            FontSize = 11,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var captureBox = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xEA, 0xF4, 0xFB)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x9C, 0xBF, 0xD6)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(9),
            Margin = new Thickness(0, 10, 0, 0)
        };
        var captureStack = new StackPanel();
        captureBox.Child = captureStack;
        captureStack.Children.Add(new TextBlock
        {
            Text = "Automatische Dokumentationsaufnahme",
            FontWeight = FontWeights.Bold
        });
        captureStack.Children.Add(new TextBlock
        {
            Text = "Erzeugt Original-PNGs direkt aus Partcounter. Geschützte Reiter werden nicht automatisch entsperrt; bei Bedarf vorher einmal freigeben.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x42, 0x54, 0x66)),
            Margin = new Thickness(0, 3, 0, 6)
        });
        var captureButtons = new WrapPanel();
        _autoCaptureButton.Content = "Screenshots automatisch erstellen";
        _autoCaptureButton.Padding = new Thickness(9, 5, 9, 5);
        _autoCaptureButton.ToolTip = "Priorität-A-Screenshots automatisch aus der laufenden Partcounter-Oberfläche erzeugen";
        _openCaptureFolderButton.Content = "Ordner öffnen";
        _openCaptureFolderButton.Padding = new Thickness(9, 5, 9, 5);
        captureButtons.Children.Add(_autoCaptureButton);
        captureButtons.Children.Add(_openCaptureFolderButton);
        captureStack.Children.Add(captureButtons);
        _captureStatus.Text = $"Ziel: {DocumentationCaptureService.ScreenshotDirectory}";
        _captureStatus.TextWrapping = TextWrapping.Wrap;
        _captureStatus.FontSize = 10.5;
        _captureStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x71, 0x80));
        _captureStatus.Margin = new Thickness(0, 5, 0, 0);
        captureStack.Children.Add(_captureStatus);
        filters.Children.Add(captureBox);

        leftDock.Children.Add(filters);

        _topicList.ItemsSource = _visibleTopics;
        _topicList.DisplayMemberPath = nameof(HelpTopic.Title);
        _topicList.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        leftDock.Children.Add(_topicList);

        var right = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xDE, 0xE6)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16)
        };
        Grid.SetColumn(right, 2);
        root.Children.Add(right);

        var rightGrid = new Grid();
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.Child = rightGrid;

        _title.FontSize = 25;
        _title.FontWeight = FontWeights.Bold;
        _title.TextWrapping = TextWrapping.Wrap;
        rightGrid.Children.Add(_title);

        _category.Foreground = new SolidColorBrush(Color.FromRgb(0x56, 0x64, 0x73));
        _category.Margin = new Thickness(0, 3, 0, 10);
        Grid.SetRow(_category, 1);
        rightGrid.Children.Add(_category);

        BuildScreenshotPanel();
        Grid.SetRow(_screenshotBorder, 2);
        rightGrid.Children.Add(_screenshotBorder);

        _body.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _body.IsToolBarVisible = false;
        _body.Margin = new Thickness(0, 6, 0, 0);
        Grid.SetRow(_body, 3);
        rightGrid.Children.Add(_body);

        var dependencyBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xEF, 0xF4, 0xF8)),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 12, 0, 0)
        };
        Grid.SetRow(dependencyBorder, 4);
        rightGrid.Children.Add(dependencyBorder);

        var deps = new StackPanel();
        dependencyBorder.Child = deps;
        deps.Children.Add(new TextBlock
        {
            Text = "Funktionsabhängigkeiten",
            FontWeight = FontWeights.Bold,
            FontSize = 14
        });
        deps.Children.Add(new TextBlock { Text = "Benötigt / hängt ab von:", Margin = new Thickness(0, 6, 0, 2) });
        deps.Children.Add(_dependsPanel);
        deps.Children.Add(new TextBlock { Text = "Wirkt weiter auf / wird verwendet von:", Margin = new Thickness(0, 8, 0, 2) });
        deps.Children.Add(_usedByPanel);
        deps.Children.Add(new TextBlock
        {
            Text = "Die Schaltflächen springen direkt zum verknüpften Hilfethema.",
            Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x71, 0x80)),
            FontSize = 11,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });

        return root;
    }

    private void BuildScreenshotPanel()
    {
        _screenshotBorder.Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xF9, 0xFB));
        _screenshotBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xDE, 0xE6));
        _screenshotBorder.BorderThickness = new Thickness(1);
        _screenshotBorder.CornerRadius = new CornerRadius(5);
        _screenshotBorder.Padding = new Thickness(10);
        _screenshotBorder.Margin = new Thickness(0, 0, 0, 8);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _screenshotBorder.Child = root;

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _screenshotTitle.Text = "Original-Screenshot";
        _screenshotTitle.FontWeight = FontWeights.Bold;
        _screenshotTitle.FontSize = 14;
        header.Children.Add(_screenshotTitle);
        _copyScreenshotInstructionButton.Content = "Aufnahmeanweisung kopieren";
        _copyScreenshotInstructionButton.Padding = new Thickness(8, 3, 8, 3);
        _copyScreenshotInstructionButton.FontSize = 11;
        Grid.SetColumn(_copyScreenshotInstructionButton, 1);
        header.Children.Add(_copyScreenshotInstructionButton);
        root.Children.Add(header);

        _screenshotImage.Stretch = Stretch.Uniform;
        _screenshotImage.MaxHeight = 300;
        _screenshotImage.HorizontalAlignment = HorizontalAlignment.Left;
        _screenshotImage.Margin = new Thickness(0, 8, 0, 4);
        Grid.SetRow(_screenshotImage, 1);
        root.Children.Add(_screenshotImage);

        _screenshotPlaceholder.TextWrapping = TextWrapping.Wrap;
        _screenshotPlaceholder.Foreground = new SolidColorBrush(Color.FromRgb(0x56, 0x64, 0x73));
        _screenshotPlaceholder.Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xF3, 0xF7));
        _screenshotPlaceholder.Padding = new Thickness(10);
        _screenshotPlaceholder.Margin = new Thickness(0, 8, 0, 4);
        Grid.SetRow(_screenshotPlaceholder, 1);
        root.Children.Add(_screenshotPlaceholder);

        _screenshotCaption.TextWrapping = TextWrapping.Wrap;
        _screenshotCaption.Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x71, 0x80));
        _screenshotCaption.FontSize = 11;
        Grid.SetRow(_screenshotCaption, 2);
        root.Children.Add(_screenshotCaption);
    }

    private void RefreshFilter()
    {
        var previousId = (_topicList.SelectedItem as HelpTopic)?.Id;
        _visibleTopics.Clear();
        foreach (var topic in _help.Filter(_searchBox.Text, _categoryBox.SelectedItem?.ToString()))
            _visibleTopics.Add(topic);

        var selection = previousId is null ? null : _visibleTopics.FirstOrDefault(t => t.Id == previousId);
        _topicList.SelectedItem = selection ?? _visibleTopics.FirstOrDefault();
        if (_visibleTopics.Count == 0)
            ShowTopic(null);
    }

    private void ShowTopic(HelpTopic? topic)
    {
        _currentTopic = topic;
        if (topic is null)
        {
            _title.Text = "Kein Hilfethema gefunden";
            _category.Text = "Suchbegriff oder Kategorie ändern.";
            _body.Document = HelpDocumentRenderer.Build(string.Empty);
            _dependsPanel.Children.Clear();
            _usedByPanel.Children.Clear();
            _screenshotBorder.Visibility = Visibility.Collapsed;
            return;
        }

        _title.Text = topic.Title;
        _category.Text = $"Kategorie: {topic.Category} · Thema: {topic.Id}";
        _body.Document = HelpDocumentRenderer.Build(topic.Body);
        PopulateLinks(_dependsPanel, topic.DependsOn);
        PopulateLinks(_usedByPanel, topic.UsedBy);
        ShowScreenshot(topic);
    }

    private void ShowScreenshot(HelpTopic topic)
    {
        if (!topic.HasScreenshotSlot)
        {
            _screenshotBorder.Visibility = Visibility.Collapsed;
            return;
        }

        _screenshotBorder.Visibility = Visibility.Visible;
        _screenshotTitle.Text = $"Original-Screenshot · {topic.ScreenshotFileName}";
        _screenshotCaption.Text = topic.ScreenshotInstruction;
        _copyScreenshotInstructionButton.IsEnabled = !string.IsNullOrWhiteSpace(topic.ScreenshotInstruction);

        var bitmap = TryLoadScreenshot(topic.ScreenshotFileName);
        if (bitmap is not null)
        {
            _screenshotImage.Source = bitmap;
            _screenshotImage.Visibility = Visibility.Visible;
            _screenshotPlaceholder.Visibility = Visibility.Collapsed;
            return;
        }

        _screenshotImage.Source = null;
        _screenshotImage.Visibility = Visibility.Collapsed;
        _screenshotPlaceholder.Visibility = Visibility.Visible;
        _screenshotPlaceholder.Text =
            $"Screenshot-Slot vorbereitet.\n\nDatei: {topic.ScreenshotFileName}\n\n{topic.ScreenshotInstruction}\n\n" +
            "R001.20 kann die wichtigsten Screenshots automatisch erzeugen. Alternativ wird ein eingebetteter Screenshot aus Help/Screenshots verwendet.";
    }

    private static BitmapImage? TryLoadScreenshot(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var externalPath = Path.Combine(DocumentationCaptureService.ScreenshotDirectory, fileName);
        if (File.Exists(externalPath))
        {
            try
            {
                using var stream = File.OpenRead(externalPath);
                var external = new BitmapImage();
                external.BeginInit();
                external.CacheOption = BitmapCacheOption.OnLoad;
                external.StreamSource = stream;
                external.EndInit();
                external.Freeze();
                return external;
            }
            catch
            {
                // Fall through to embedded resource.
            }
        }

        try
        {
            var uri = new Uri($"pack://application:,,,/Partcounter;component/Help/Screenshots/{fileName}", UriKind.Absolute);
            var resource = Application.GetResourceStream(uri);
            if (resource?.Stream is null)
                return null;

            using var stream = resource.Stream;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private async Task RunAutomaticCaptureAsync()
    {
        if (Owner is not MainWindow mainWindow)
        {
            MessageBox.Show(
                "Das Hilfezentrum wurde ohne Partcounter-Hauptfenster geöffnet. Die automatische Aufnahme kann deshalb nicht gestartet werden.",
                "Partcounter Dokumentationsaufnahme",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var answer = MessageBox.Show(
            "Partcounter erzeugt jetzt automatisch Original-Screenshots der verfügbaren Programmoberflächen.\n\n" +
            "Es werden keine geschützten Reiter automatisch entsperrt, keine Modbus-Schreibbefehle gesendet und keine echten ALS-Fehler provoziert. " +
            "Falls Admin-Bereiche mit aufgenommen werden sollen, diese bitte vorher freigeben.\n\nAufnahme jetzt starten?",
            "Automatische Dokumentationsaufnahme",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
            return;

        _autoCaptureButton.IsEnabled = false;
        _captureStatus.Text = "Automatische Aufnahme läuft …";

        try
        {
            var service = new DocumentationCaptureService();
            var result = await service.CapturePriorityAAsync(
                mainWindow,
                this,
                text => _captureStatus.Text = text);

            _captureStatus.Text =
                $"Fertig: {result.CapturedCount} PNGs · ZIP: {result.ZipPath}" +
                (result.SkippedItems.Count > 0 ? $" · {result.SkippedItems.Count} ausgelassen" : string.Empty);

            if (_currentTopic is not null)
                ShowScreenshot(_currentTopic);

            MessageBox.Show(
                $"Automatische Dokumentationsaufnahme abgeschlossen.\n\n" +
                $"Erstellt: {result.CapturedCount} Screenshots\n" +
                $"Ausgelassen: {result.SkippedItems.Count}\n\n" +
                $"Screenshot-Ordner:\n{result.ScreenshotDirectory}\n\nZIP-Paket:\n{result.ZipPath}",
                "Partcounter Dokumentationsaufnahme",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _captureStatus.Text = $"Aufnahme fehlgeschlagen: {ex.Message}";
            MessageBox.Show(
                ex.Message,
                "Partcounter Dokumentationsaufnahme",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _autoCaptureButton.IsEnabled = true;
        }
    }

    private static void OpenCaptureFolder()
    {
        try
        {
            Directory.CreateDirectory(DocumentationCaptureService.ScreenshotDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = DocumentationCaptureService.ScreenshotDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Screenshot-Ordner", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CopyScreenshotInstruction()
    {
        if (_currentTopic is null || string.IsNullOrWhiteSpace(_currentTopic.ScreenshotInstruction))
            return;

        try
        {
            Clipboard.SetText($"{_currentTopic.ScreenshotFileName}\r\n{_currentTopic.ScreenshotInstruction}");
        }
        catch
        {
            // Clipboard failure must not affect help usage.
        }
    }

    private void PopulateLinks(Panel panel, IReadOnlyList<string> ids)
    {
        panel.Children.Clear();
        if (ids.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = "–", Foreground = Brushes.Gray });
            return;
        }

        foreach (var id in ids)
        {
            var target = _help.Find(id);
            var button = new Button
            {
                Content = target?.Title ?? id,
                Tag = id,
                Margin = new Thickness(2),
                Padding = new Thickness(7, 3, 7, 3),
                ToolTip = $"Zum Hilfethema '{target?.Title ?? id}' springen"
            };
            button.Click += (_, _) => OpenTopic((string)button.Tag);
            panel.Children.Add(button);
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            _searchBox.Focus();
            _searchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
