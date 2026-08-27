using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
    private readonly TextBox _body = new();
    private readonly WrapPanel _dependsPanel = new();
    private readonly WrapPanel _usedByPanel = new();

    public HelpCenterWindow()
    {
        Title = "Partcounter R001.14 – Hilfe";
        Width = 1320;
        Height = 850;
        MinWidth = 1000;
        MinHeight = 650;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0xF2, 0xF4, 0xF7));

        Content = BuildUi();
        _searchBox.TextChanged += (_, _) => RefreshFilter();
        _categoryBox.SelectionChanged += (_, _) => RefreshFilter();
        _topicList.SelectionChanged += (_, _) => ShowTopic(_topicList.SelectedItem as HelpTopic);
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
        _topicList.ScrollIntoView(_topicList.SelectedItem);
    }

    private UIElement BuildUi()
    {
        var root = new Grid { Margin = new Thickness(12) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
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
            Text = "Funktion, Begriff oder Zusammenhang suchen",
            Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x71, 0x80)),
            Margin = new Thickness(0, 3, 0, 6)
        });
        _searchBox.MinHeight = 30;
        _searchBox.ToolTip = "Suche in Titel, Beschreibung, Schlagwörtern und Abhängigkeiten";
        filters.Children.Add(_searchBox);
        filters.Children.Add(new TextBlock { Text = "Kategorie", Margin = new Thickness(0, 8, 0, 3), FontWeight = FontWeights.SemiBold });
        _categoryBox.MinHeight = 30;
        filters.Children.Add(_categoryBox);
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

        _body.IsReadOnly = true;
        _body.BorderThickness = new Thickness(0);
        _body.Background = Brushes.Transparent;
        _body.TextWrapping = TextWrapping.Wrap;
        _body.AcceptsReturn = true;
        _body.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _body.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _body.FontFamily = new FontFamily("Segoe UI");
        _body.FontSize = 14;
        _body.Padding = new Thickness(0, 0, 8, 0);
        Grid.SetRow(_body, 2);
        rightGrid.Children.Add(_body);

        var dependencyBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xEF, 0xF4, 0xF8)),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 12, 0, 0)
        };
        Grid.SetRow(dependencyBorder, 3);
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
            Text = "Tipp: Die Abhängigkeits-Schaltflächen sind anklickbar und springen direkt zum verknüpften Hilfethema.",
            Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x71, 0x80)),
            FontSize = 11,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });

        return root;
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
        if (topic is null)
        {
            _title.Text = "Kein Hilfethema gefunden";
            _category.Text = "Suchbegriff oder Kategorie ändern.";
            _body.Text = string.Empty;
            _dependsPanel.Children.Clear();
            _usedByPanel.Children.Clear();
            return;
        }

        _title.Text = topic.Title;
        _category.Text = $"Kategorie: {topic.Category} · Thema: {topic.Id}";
        _body.Text = topic.Body;
        PopulateLinks(_dependsPanel, topic.DependsOn);
        PopulateLinks(_usedByPanel, topic.UsedBy);
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
