using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Partcounter.Services;

namespace Partcounter.Views;

public sealed class AboutWindow : Window
{
    public AboutWindow()
    {
        Title = "Über Partcounter";
        Width = 760;
        Height = 720;
        MinWidth = 650;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0xF2, 0xF4, 0xF7));
        Content = BuildUi();
    }

    private UIElement BuildUi()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "–";
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? version;
        var dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Partcounter");
        var dbPath = Path.Combine(dataDirectory, "partcounter.db");

        var root = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var stack = new StackPanel { Margin = new Thickness(22) };
        root.Content = stack;

        stack.Children.Add(new TextBlock
        {
            Text = "PARTCOUNTER",
            FontSize = 34,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x18, 0x21, 0x2B))
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Industrieller VE-Leitstand für Spritzgussmaschinen · Siemens LOGO! · Modbus TCP",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(0x56, 0x64, 0x73)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 16)
        });

        stack.Children.Add(Section("Produkt"));
        stack.Children.Add(InfoGrid(new[]
        {
            ("Aktuelle Revision", "R001.19"),
            ("Assembly-Version", version),
            ("Build-Information", informational),
            ("Programmierer", "Harald Peetz"),
            ("Technologie", "C# · .NET 8 · WPF · SQLite · NModbus"),
            ("Modbus-Protokoll", $"Partcounter Protocol V{ModbusRegisterMap.ProtocolVersion}"),
            ("LOGO!-Programm", "Partcounter_LOGO_V001"),
            ("Produktionsschutz", "Tägliche SQLite-Sicherung · Integritätsprüfung · Diagnosepaket")
        }));

        stack.Children.Add(Section("Systeminformationen"));
        stack.Children.Add(InfoGrid(new[]
        {
            ("Windows / OS", RuntimeInformation.OSDescription),
            (".NET Runtime", RuntimeInformation.FrameworkDescription),
            ("Prozessarchitektur", RuntimeInformation.ProcessArchitecture.ToString()),
            ("Betriebssystemarchitektur", RuntimeInformation.OSArchitecture.ToString()),
            ("64-Bit-Prozess", Environment.Is64BitProcess ? "Ja" : "Nein"),
            ("Computer", Environment.MachineName),
            ("Programmordner", AppContext.BaseDirectory),
            ("Partcounter-Daten", dataDirectory),
            ("SQLite-Datenbank", dbPath)
        }));

        stack.Children.Add(Section("Lizenzhinweis"));
        stack.Children.Add(new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xDE, 0xE6)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Child = new TextBlock
            {
                Text = "© 2026 Harald Peetz. Alle Rechte vorbehalten.\n\nPartcounter ist proprietäre Software. Nutzung, Vervielfältigung, Änderung oder Weitergabe außerhalb einer ausdrücklich eingeräumten betrieblichen Lizenz ist nicht gestattet. Dieser Kurztext dient der Produktkennzeichnung und ersetzt keine vollständige Lizenzvereinbarung.\n\nPartcounter und Partcounter_LOGO_V001 sind keine Sicherheitssteuerung. Safety-Funktionen der Maschine bleiben in den dafür vorgesehenen sicheren Systemen.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(0x42, 0x54, 0x66))
            }
        });

        var buttons = new WrapPanel { Margin = new Thickness(0, 16, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        var copy = new Button { Content = "Systeminfo kopieren", Padding = new Thickness(12, 6, 12, 6) };
        copy.Click += (_, _) => Clipboard.SetText(BuildSystemInfo(version, informational, dataDirectory, dbPath));
        var close = new Button { Content = "Schließen", Padding = new Thickness(12, 6, 12, 6), IsDefault = true };
        close.Click += (_, _) => Close();
        buttons.Children.Add(copy);
        buttons.Children.Add(close);
        stack.Children.Add(buttons);

        return root;
    }

    private static TextBlock Section(string text) => new()
    {
        Text = text,
        FontSize = 18,
        FontWeight = FontWeights.Bold,
        Margin = new Thickness(0, 14, 0, 7)
    };

    private static Grid InfoGrid(IEnumerable<(string Label, string Value)> rows)
    {
        var grid = new Grid
        {
            Background = Brushes.White,
            Margin = new Thickness(0, 0, 0, 2)
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var index = 0;
        foreach (var row in rows)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = new TextBlock
            {
                Text = row.Label,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(10, 5, 10, 5),
                VerticalAlignment = VerticalAlignment.Top
            };
            var value = new TextBlock
            {
                Text = row.Value,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10, 5, 10, 5),
                Foreground = new SolidColorBrush(Color.FromRgb(0x42, 0x54, 0x66))
            };
            Grid.SetRow(label, index);
            Grid.SetRow(value, index);
            Grid.SetColumn(value, 1);
            grid.Children.Add(label);
            grid.Children.Add(value);
            index++;
        }

        return grid;
    }

    private static string BuildSystemInfo(string version, string informational, string dataDirectory, string dbPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Partcounter R001.19");
        sb.AppendLine($"Assembly: {version}");
        sb.AppendLine($"Build: {informational}");
        sb.AppendLine("Programmierer: Harald Peetz");
        sb.AppendLine($"Modbus Protocol: V{ModbusRegisterMap.ProtocolVersion}");
        sb.AppendLine("Produktionsschutz: tägliche SQLite-Sicherung, Integritätsprüfung, Diagnosepaket");
        sb.AppendLine($"OS: {RuntimeInformation.OSDescription}");
        sb.AppendLine($".NET: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"Process: {RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine($"OS Architecture: {RuntimeInformation.OSArchitecture}");
        sb.AppendLine($"Computer: {Environment.MachineName}");
        sb.AppendLine($"App: {AppContext.BaseDirectory}");
        sb.AppendLine($"Data: {dataDirectory}");
        sb.AppendLine($"DB: {dbPath}");
        return sb.ToString();
    }
}
