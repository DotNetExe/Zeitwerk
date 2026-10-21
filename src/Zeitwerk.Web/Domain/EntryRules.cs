namespace Zeitwerk.Web.Domain;

/// <summary>Prüfregeln für manuell angelegte oder bearbeitete Zeiteinträge.</summary>
public static class EntryRules
{
    /// <returns>Fehlermeldung oder <c>null</c>, wenn der Eintrag gültig ist.</returns>
    public static string? Validate(DateTimeOffset start, DateTimeOffset? end, IEnumerable<TimeEntry> others)
    {
        if (!TimeGrid.IsOnGrid(start) || (end is { } e && !TimeGrid.IsOnGrid(e)))
            return "Beginn und Ende müssen im 5-Minuten-Raster liegen.";
        if (end <= start)
            return "Das Ende muss nach dem Beginn liegen.";

        var conflict = others.FirstOrDefault(o => Overlaps(start, end, o.Start, o.End));
        return conflict is null ? null : "Der Eintrag überschneidet sich mit einem anderen Eintrag.";
    }

    /// <summary>Halboffene Intervalle [Beginn, Ende); ein offenes Ende reicht bis unendlich.</summary>
    public static bool Overlaps(DateTimeOffset aStart, DateTimeOffset? aEnd, DateTimeOffset bStart, DateTimeOffset? bEnd) =>
        (aEnd is null || bStart < aEnd) && (bEnd is null || aStart < bEnd);
}
