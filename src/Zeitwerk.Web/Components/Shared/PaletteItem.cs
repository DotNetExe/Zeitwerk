namespace Zeitwerk.Web.Components.Shared;

/// <summary>Ein Eintrag der Command-Palette: Projekt, Projekt/Tätigkeit-Kombination oder Befehl.</summary>
public sealed record PaletteItem(string Group, string Title, string? Subtitle, string Icon, string? Color, Func<Task> Execute)
{
    /// <summary>Zusätzliche Suchbegriffe, die nicht angezeigt werden.</summary>
    public string? Keywords { get; init; }

    public string SearchText => $"{Title} {Subtitle} {Keywords}";

    /// <summary>
    /// Ohne Suchtext die Einträge in der gegebenen Reihenfolge, sonst die Treffer nach Güte
    /// (bei Gleichstand in der gegebenen Reihenfolge).
    /// </summary>
    public static List<PaletteItem> Filter(IEnumerable<PaletteItem> items, string? query, int max) =>
        items.Select((item, index) => (item, index, score: FuzzyMatch.Score(query, item.SearchText)))
            .Where(x => x.score is not null)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.index)
            .Take(max)
            .Select(x => x.item)
            .ToList();
}
