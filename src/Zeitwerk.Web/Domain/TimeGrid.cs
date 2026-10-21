namespace Zeitwerk.Web.Domain;

/// <summary>Das Fünf-Minuten-Raster, auf dem jeder Beginn und jedes Ende liegt.</summary>
public static class TimeGrid
{
    public static readonly TimeSpan Slot = TimeSpan.FromMinutes(5);

    public static DateTimeOffset Floor(DateTimeOffset t) =>
        new(t.UtcTicks - t.UtcTicks % Slot.Ticks, TimeSpan.Zero);

    /// <summary>Aufrunden auf die nächste Rastergrenze; liegt <paramref name="t"/> höchstens
    /// <paramref name="tolerance"/> hinter einer Grenze, zählt diese.</summary>
    public static DateTimeOffset Ceil(DateTimeOffset t, TimeSpan tolerance)
    {
        var floor = Floor(t);
        return t - floor <= tolerance ? floor : floor + Slot;
    }

    public static bool IsOnGrid(DateTimeOffset t) => t.UtcTicks % Slot.Ticks == 0;
}
