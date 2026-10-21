namespace Zeitwerk.Web.Domain;

public static class ProjectColors
{
    /// <summary>Gut unterscheidbare Farben, die auf dunklem Hintergrund lesbar sind.</summary>
    public static readonly IReadOnlyList<string> Palette =
    [
        "#4FC3F7", "#81C784", "#FFB74D", "#BA68C8", "#E57373", "#4DB6AC",
        "#FFD54F", "#7986CB", "#F06292", "#AED581", "#90A4AE", "#FF8A65",
    ];

    /// <summary>Schlägt die am seltensten verwendete Palettenfarbe vor (bei Gleichstand die erste).</summary>
    public static string Suggest(IEnumerable<string> usedColors)
    {
        var counts = usedColors
            .GroupBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        return Palette.MinBy(c => counts.GetValueOrDefault(c))!;
    }
}
