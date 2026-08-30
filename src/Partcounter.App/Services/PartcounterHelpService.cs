using System.Reflection;

namespace Partcounter.Services;

public sealed record HelpTopic(
    string Id,
    string Title,
    string Category,
    string Body,
    IReadOnlyList<string> DependsOn,
    IReadOnlyList<string> UsedBy,
    IReadOnlyList<string> Keywords,
    string ScreenshotFileName,
    string ScreenshotInstruction)
{
    public string SearchText => string.Join(' ', new[]
        {
            Id, Title, Category, Body, ScreenshotFileName, ScreenshotInstruction
        }
        .Concat(Keywords)
        .Concat(DependsOn)
        .Concat(UsedBy));

    public bool HasScreenshotSlot => !string.IsNullOrWhiteSpace(ScreenshotFileName);
}

public sealed class PartcounterHelpService
{
    private const string ResourceMarker = "PARTCOUNTER_HILFE_R001_25";
    private IReadOnlyList<HelpTopic>? _topics;

    public IReadOnlyList<HelpTopic> Topics => _topics ??= LoadTopics();

    public HelpTopic? Find(string id) => Topics.FirstOrDefault(t =>
        string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<HelpTopic> Filter(string? query, string? category)
    {
        IEnumerable<HelpTopic> source = Topics;
        if (!string.IsNullOrWhiteSpace(category) && !string.Equals(category, "Alle", StringComparison.OrdinalIgnoreCase))
            source = source.Where(t => string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query))
        {
            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            source = source.Where(t => terms.All(term =>
                t.SearchText.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        return source.OrderBy(t => t.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> Categories => new[] { "Alle" }
        .Concat(Topics.Select(t => t.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        .ToList();

    private static IReadOnlyList<HelpTopic> LoadTopics()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(ResourceMarker, StringComparison.OrdinalIgnoreCase)
                           && name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (resources.Count == 0)
            return Array.Empty<HelpTopic>();

        var documents = new List<string>(resources.Count);
        foreach (var resource in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream is null)
                continue;
            using var reader = new StreamReader(stream);
            documents.Add(reader.ReadToEnd());
        }

        if (documents.Count == 0)
            return Array.Empty<HelpTopic>();

        var text = string.Join("\n\n", documents).Replace("\r\n", "\n");
        var lines = text.Split('\n');
        var result = new List<HelpTopic>();

        string? id = null;
        string? title = null;
        string category = "Sonstiges";
        var depends = new List<string>();
        var usedBy = new List<string>();
        var keywords = new List<string>();
        var body = new List<string>();
        var screenshotFileName = string.Empty;
        var screenshotInstruction = string.Empty;
        var inBody = false;

        void Flush()
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
                return;

            if (result.Any(topic => string.Equals(topic.Id, id, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Doppelte Hilfe-Themen-ID in R001.25: {id}");

            var bodyText = string.Join(Environment.NewLine, body).Trim();
            result.Add(new HelpTopic(
                id,
                title,
                category,
                bodyText,
                depends.ToList(),
                usedBy.ToList(),
                keywords.ToList(),
                screenshotFileName,
                screenshotInstruction));
        }

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (line.StartsWith("## [", StringComparison.Ordinal))
            {
                Flush();
                var close = line.IndexOf(']');
                id = close > 4 ? line[4..close].Trim() : Guid.NewGuid().ToString("N");
                title = close >= 0 ? line[(close + 1)..].Trim() : line.TrimStart('#', ' ');
                category = "Sonstiges";
                depends = new List<string>();
                usedBy = new List<string>();
                keywords = new List<string>();
                body = new List<string>();
                screenshotFileName = string.Empty;
                screenshotInstruction = string.Empty;
                inBody = false;
                continue;
            }

            if (id is null)
                continue;

            if (!inBody)
            {
                if (line == "---")
                {
                    inBody = true;
                    continue;
                }
                if (line.StartsWith("Kategorie:", StringComparison.OrdinalIgnoreCase))
                {
                    category = line["Kategorie:".Length..].Trim();
                    continue;
                }
                if (line.StartsWith("Abhängigkeiten:", StringComparison.OrdinalIgnoreCase))
                {
                    depends = ParseList(line["Abhängigkeiten:".Length..]);
                    continue;
                }
                if (line.StartsWith("Folgewirkungen:", StringComparison.OrdinalIgnoreCase))
                {
                    usedBy = ParseList(line["Folgewirkungen:".Length..]);
                    continue;
                }
                if (line.StartsWith("Schlagwörter:", StringComparison.OrdinalIgnoreCase))
                {
                    keywords = ParseList(line["Schlagwörter:".Length..]);
                    continue;
                }
                if (line.StartsWith("Screenshot:", StringComparison.OrdinalIgnoreCase))
                {
                    screenshotFileName = line["Screenshot:".Length..].Trim();
                    continue;
                }
                if (line.StartsWith("Screenshot-Hinweis:", StringComparison.OrdinalIgnoreCase))
                {
                    screenshotInstruction = line["Screenshot-Hinweis:".Length..].Trim();
                    continue;
                }
            }
            else
            {
                body.Add(line);
            }
        }

        Flush();
        return result;
    }

    private static List<string> ParseList(string source) => source
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(x => x != "-")
        .ToList();
}
