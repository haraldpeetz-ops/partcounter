using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Partcounter.Services;

public static class HelpDocumentRenderer
{
    private static readonly Regex NumberedLine = new(@"^(?<number>\d+)\.\s+(?<text>.+)$", RegexOptions.Compiled);

    public static FlowDocument Build(string? source)
    {
        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(0x18, 0x21, 0x2B)),
            PagePadding = new Thickness(0, 0, 12, 0),
            LineHeight = 21
        };

        var lines = (source ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                document.Blocks.Add(new Paragraph { Margin = new Thickness(0, 3, 0, 3) });
                continue;
            }

            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                document.Blocks.Add(Heading(line[4..], 19));
                continue;
            }

            if (line.StartsWith("#### ", StringComparison.Ordinal))
            {
                document.Blocks.Add(Heading(line[5..], 16));
                continue;
            }

            if (TryBuildCallout(line, out var callout))
            {
                document.Blocks.Add(callout);
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                var paragraph = new Paragraph { Margin = new Thickness(18, 2, 0, 2), TextIndent = -12 };
                paragraph.Inlines.Add(new Run("• ") { FontWeight = FontWeights.Bold });
                AddInlineFormatting(paragraph, line[2..]);
                document.Blocks.Add(paragraph);
                continue;
            }

            var numbered = NumberedLine.Match(line);
            if (numbered.Success)
            {
                var paragraph = new Paragraph { Margin = new Thickness(18, 3, 0, 3), TextIndent = -18 };
                paragraph.Inlines.Add(new Run(numbered.Groups["number"].Value + ". ") { FontWeight = FontWeights.Bold });
                AddInlineFormatting(paragraph, numbered.Groups["text"].Value);
                document.Blocks.Add(paragraph);
                continue;
            }

            if (line == "---")
            {
                document.Blocks.Add(new Paragraph(new Run("────────────────────────────────"))
                {
                    Foreground = Brushes.LightGray,
                    Margin = new Thickness(0, 6, 0, 6)
                });
                continue;
            }

            var normal = new Paragraph { Margin = new Thickness(0, 2, 0, 7) };
            AddInlineFormatting(normal, line);
            document.Blocks.Add(normal);
        }

        return document;
    }

    private static Paragraph Heading(string text, double size) => new(new Run(text.Trim()))
    {
        FontSize = size,
        FontWeight = FontWeights.Bold,
        Foreground = new SolidColorBrush(Color.FromRgb(0x18, 0x21, 0x2B)),
        Margin = new Thickness(0, 12, 0, 5)
    };

    private static bool TryBuildCallout(string line, out Paragraph paragraph)
    {
        paragraph = null!;
        var kinds = new[]
        {
            (Prefix: "[WICHTIG]", Label: "WICHTIG", Background: Color.FromRgb(0xEF, 0xF4, 0xF8), Foreground: Color.FromRgb(0x24, 0x4E, 0x68)),
            (Prefix: "[WARNUNG]", Label: "WARNUNG", Background: Color.FromRgb(0xFF, 0xF0, 0xE3), Foreground: Color.FromRgb(0xA1, 0x4A, 0x00)),
            (Prefix: "[PRAXIS]", Label: "PRAXIS", Background: Color.FromRgb(0xEE, 0xF6, 0xE9), Foreground: Color.FromRgb(0x36, 0x67, 0x2A)),
            (Prefix: "[TIPP]", Label: "TIPP", Background: Color.FromRgb(0xF3, 0xF0, 0xFA), Foreground: Color.FromRgb(0x55, 0x3A, 0x83))
        };

        foreach (var kind in kinds)
        {
            if (!line.StartsWith(kind.Prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            paragraph = new Paragraph
            {
                Background = new SolidColorBrush(kind.Background),
                Foreground = new SolidColorBrush(kind.Foreground),
                Padding = new Thickness(10, 7, 10, 7),
                Margin = new Thickness(0, 7, 0, 9)
            };
            paragraph.Inlines.Add(new Run(kind.Label + ": ") { FontWeight = FontWeights.Bold });
            AddInlineFormatting(paragraph, line[kind.Prefix.Length..].Trim());
            return true;
        }

        return false;
    }

    private static void AddInlineFormatting(Paragraph paragraph, string text)
    {
        var index = 0;
        while (index < text.Length)
        {
            var boldStart = text.IndexOf("**", index, StringComparison.Ordinal);
            var codeStart = text.IndexOf('`', index);
            var next = MinPositive(boldStart, codeStart);

            if (next < 0)
            {
                paragraph.Inlines.Add(new Run(text[index..]));
                break;
            }

            if (next > index)
                paragraph.Inlines.Add(new Run(text[index..next]));

            if (next == boldStart)
            {
                var end = text.IndexOf("**", boldStart + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    paragraph.Inlines.Add(new Run(text[boldStart..]));
                    break;
                }

                paragraph.Inlines.Add(new Run(text[(boldStart + 2)..end]) { FontWeight = FontWeights.Bold });
                index = end + 2;
            }
            else
            {
                var end = text.IndexOf('`', codeStart + 1);
                if (end < 0)
                {
                    paragraph.Inlines.Add(new Run(text[codeStart..]));
                    break;
                }

                paragraph.Inlines.Add(new Run(text[(codeStart + 1)..end])
                {
                    FontFamily = new FontFamily("Consolas"),
                    Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF2, 0xF4))
                });
                index = end + 1;
            }
        }
    }

    private static int MinPositive(int first, int second)
    {
        if (first < 0) return second;
        if (second < 0) return first;
        return Math.Min(first, second);
    }
}
