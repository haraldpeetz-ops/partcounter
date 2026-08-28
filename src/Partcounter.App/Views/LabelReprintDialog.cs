using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Partcounter.Models;

namespace Partcounter.Views;

public sealed class LabelReprintDialog : Window
{
    private readonly ComboBox _reasonBox;
    private readonly TextBox _noteBox;

    public LabelReprintDialog(PackagingUnitRecord record, int successfulReprintCount)
    {
        Title = "Partcounter – Etikett nachdrucken";
        Width = 610;
        Height = 520;
        MinWidth = 560;
        MinHeight = 470;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        Background = new SolidColorBrush(Color.FromRgb(0xF2, 0xF4, 0xF7));

        _reasonBox = new ComboBox
        {
            MinHeight = 32,
            Margin = new Thickness(0, 4, 0, 0),
            ItemsSource = new[]
            {
                "Etikett verloren",
                "Etikett beschädigt",
                "Etikett unleserlich",
                "Druckfehler / Fehldruck",
                "Etikett bei Verpackung beschädigt",
                "Sonstiger Grund"
            },
            SelectedIndex = 0
        };

        _noteBox = new TextBox
        {
            MinHeight = 70,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 4, 0, 0)
        };

        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel();
        header.Children.Add(new TextBlock
        {
            Text = "Etikett nachdrucken",
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x18, 0x21, 0x2B))
        });
        header.Children.Add(new TextBlock
        {
            Text = "Der Nachdruck verwendet exakt den ursprünglichen VE-Datensatz. Es wird keine neue VE und keine neue VE-ID erzeugt.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x56, 0x64, 0x73)),
            Margin = new Thickness(0, 5, 0, 12)
        });
        root.Children.Add(header);

        var body = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 8, 0, 8)
        };
        Grid.SetRow(body, 1);
        var stack = new StackPanel();
        body.Content = stack;

        var info = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xDE, 0xE6)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12)
        };
        var infoStack = new StackPanel();
        info.Child = infoStack;
        infoStack.Children.Add(Line("VE-ID", record.Id));
        infoStack.Children.Add(Line("Maschine / VE", $"M{record.MachineNumber:00} · VE {record.VeNumber}"));
        infoStack.Children.Add(Line("Auftrag", record.OrderNumber));
        infoStack.Children.Add(Line("Artikel", $"{record.ArticleNumber} · {record.ArticleDescription}"));
        infoStack.Children.Add(Line("Werkzeug / Kavitäten", $"{record.ToolNumber} · {record.Cavities}"));
        infoStack.Children.Add(Line("Menge", $"Ist {record.ActualQuantity:N0} · Soll {record.TargetQuantity:N0}"));
        infoStack.Children.Add(Line("VE abgeschlossen", record.CompletedAtLocalText));
        infoStack.Children.Add(Line("Bisher erfolgreiche Nachdrucke", successfulReprintCount.ToString()));
        stack.Children.Add(info);

        stack.Children.Add(new TextBlock
        {
            Text = "Nachdruckgrund",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 14, 0, 0)
        });
        stack.Children.Add(_reasonBox);

        stack.Children.Add(new TextBlock
        {
            Text = "Zusätzliche Bemerkung (optional)",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 12, 0, 0)
        });
        stack.Children.Add(_noteBox);

        stack.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xF4, 0xCE)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD6, 0xA5, 0x00)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 14, 0, 0),
            Child = new TextBlock
            {
                Text = "Rückverfolgbarkeit: Jeder Nachdruckversuch wird mit Zeitpunkt, Drucker, Grund, Ergebnis und laufender Nachdrucknummer im Druckjournal gespeichert.",
                TextWrapping = TextWrapping.Wrap
            }
        });
        root.Children.Add(body);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };
        Grid.SetRow(buttons, 2);

        var cancel = new Button
        {
            Content = "Abbrechen",
            MinWidth = 100,
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 0, 8, 0),
            IsCancel = true
        };
        var print = new Button
        {
            Content = "Nachdruck ausführen",
            MinWidth = 150,
            Padding = new Thickness(12, 7, 12, 7),
            IsDefault = true
        };
        print.Click += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(print);
        root.Children.Add(buttons);

        Content = root;
    }

    public string ReprintReason
    {
        get
        {
            var reason = _reasonBox.SelectedItem?.ToString() ?? "Nicht angegeben";
            var note = _noteBox.Text.Trim();
            return string.IsNullOrWhiteSpace(note) ? reason : $"{reason} – {note}";
        }
    }

    private static FrameworkElement Line(string label, string value)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(175) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x71, 0x80))
        });
        var valueBlock = new TextBlock
        {
            Text = value,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(valueBlock, 1);
        grid.Children.Add(valueBlock);
        return grid;
    }
}
