namespace Zeitwerk.Web.Components.Shared;

/// <summary>Unscharfe Suche für die Command-Palette.</summary>
public static class FuzzyMatch
{
    /// <summary>
    /// Bewertet, wie gut <paramref name="query"/> auf <paramref name="text"/> passt; <c>null</c> = kein Treffer.
    /// Jeder durch Leerzeichen getrennte Suchbegriff muss vorkommen, mindestens als Buchstabenfolge in Reihenfolge
    /// („wbs“ findet „Website-Relaunch“). Zusammenhängende Treffer und Wortanfänge zählen mehr.
    /// </summary>
    public static int? Score(string? query, string text)
    {
        var total = 0;
        foreach (var term in (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (ScoreTerm(term, text) is not { } score)
                return null;
            total += score;
        }
        return total;
    }

    private static int? ScoreTerm(string term, string text)
    {
        var index = text.IndexOf(term, StringComparison.CurrentCultureIgnoreCase);
        if (index >= 0)
            return 100 + term.Length * 10 + (IsWordStart(text, index) ? 50 : 0);

        var score = 0;
        var previous = -2;
        foreach (var c in term)
        {
            var found = IndexOf(text, c, previous + 1);
            if (found < 0)
                return null;
            score += found == previous + 1 ? 10 : IsWordStart(text, found) ? 8 : 1;
            previous = found;
        }
        return score;
    }

    private static int IndexOf(string text, char c, int start)
    {
        var lower = char.ToLowerInvariant(c);
        for (var i = Math.Max(start, 0); i < text.Length; i++)
        {
            if (char.ToLowerInvariant(text[i]) == lower)
                return i;
        }
        return -1;
    }

    private static bool IsWordStart(string text, int index) => index == 0 || !char.IsLetterOrDigit(text[index - 1]);
}
