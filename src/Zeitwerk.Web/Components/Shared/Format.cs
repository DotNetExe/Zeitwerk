namespace Zeitwerk.Web.Components.Shared;

public static class Format
{
    /// <summary>Dauer als <c>7:45</c>.</summary>
    public static string Duration(TimeSpan d) => $"{(int)d.TotalHours}:{d.Minutes:00}";

    /// <summary>Laufende Dauer als <c>1:23:05</c>.</summary>
    public static string Running(TimeSpan d) => $"{(int)d.TotalHours}:{d.Minutes:00}:{d.Seconds:00}";

    public static string Hours(TimeSpan d) => $"{d.TotalHours:0.00} h";
}
