using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Partcounter.Models;

namespace Partcounter.Views;

public sealed class LabelReprintJournalWindow : Window
{
    public LabelReprintJournalWindow(PackagingUnitRecord record, IReadOnlyList<LabelReprintJournalEntry> entries)
    {
        Title = $"Partcounter – Druckjournal VE {record.VeNumber}";
        Width = 980;
        Height = 560;
        MinWidth = 760;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0xF2, 0xF4, 0xF7));

        var root = new Grid { Margin = new Thickness(14) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        header.Children.Add(new TextBlock
        {
            Text = "Etiketten-Nachdruckjournal",
            FontSize = 21,
            FontWeight = FontWeights.Bold
        });
        header.Children.Add(new TextBlock
        {
            Text = $"VE-ID {record.Id} · M{record.MachineNumber:00} · VE {record.VeNumber} · Auftrag {record.OrderNumber} · Artikel {record.ArticleNumber}",
            Foreground = new SolidColorBrush(Color.FromRgb(0x56, 0x64, 0x73)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        });
        root.Children.Add(header);

        var grid = new DataGrid
        {
            ItemsSource = entries,
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            SelectionMode = DataGridSelectionMode.Single
        };
        Grid.SetRow(grid, 1);
        grid.Columns.Add(new DataGridTextColumn { Header = "Nachdruck", Binding = new Binding(nameof(LabelReprintJournalEntry.ReprintNumber)) { StringFormat = "#{0}" }, Width = 90 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Zeit", Binding = new Binding(nameof(LabelReprintJournalEntry.PrintedAtLocalText)), Width = 145 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Drucker", Binding = new Binding(nameof(LabelReprintJournalEntry.PrinterName)), Width = 180 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Ergebnis", Binding = new Binding(nameof(LabelReprintJournalEntry.ResultText)), Width = 115 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Grund", Binding = new Binding(nameof(LabelReprintJournalEntry.Reason)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Fehler", Binding = new Binding(nameof(LabelReprintJournalEntry.ErrorMessage)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        root.Children.Add(grid);

        var footer = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        Grid.SetRow(footer, 2);
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(new TextBlock
        {
            Text = entries.Count == 0 ? "Für diese VE sind noch keine Nachdruckversuche protokolliert." : $"{entries.Count} protokollierte Nachdruckversuche.",
            Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x71, 0x80)),
            VerticalAlignment = VerticalAlignment.Center
        });
        var close = new Button
        {
            Content = "Schließen",
            MinWidth = 100,
            Padding = new Thickness(12, 7, 12, 7),
            IsCancel = true
        };
        Grid.SetColumn(close, 1);
        footer.Children.Add(close);
        root.Children.Add(footer);

        Content = root;
    }
}
