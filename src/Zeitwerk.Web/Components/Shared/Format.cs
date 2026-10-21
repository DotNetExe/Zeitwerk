namespace Zeitwerk.Web.Components.Shared;

public static class Format
{
    /// <summary>Dauer als <c>7:45</c>.</summary>
    public static string Duration(TimeSpan d) => $"{(int)d.TotalHours}:{d.Minutes:00}";

    /// <summary>Laufende Dauer als <c>1:23:05</c>.</summary>
    public static string Running(TimeSpan d) => $"{(int)d.TotalHours}:{d.Minutes:00}:{d.Seconds:00}";

    /// <summary>Lokaler Zeitpunkt relativ zu heute: <c>heute 17:30</c>, <c>gestern 17:30</c> oder <c>Mo 19.10. 17:30</c>.</summary>
    public static string Relative(DateTime local, DateOnly today)
    {
        var day = DateOnly.FromDateTime(local);
        var prefix = day == today ? "heute" : day == today.AddDays(-1) ? "gestern" : local.ToString("ddd dd.MM.");
        return $"{prefix} {local:HH:mm}";
    }

    public static string Hours(TimeSpan d) => $"{d.TotalHours:0.00} h";

    /// <summary>Veränderung als <c>+1:30</c>, <c>−0:45</c> oder <c>±0:00</c>.</summary>
    public static string Delta(TimeSpan d) =>
        (d > TimeSpan.Zero ? "+" : d < TimeSpan.Zero ? "−" : "±") + Duration(d.Duration());

    /// <summary>Saldo mit Vorzeichen als <c>+1:30</c> bzw. <c>−0:45</c>.</summary>
    public static string Balance(TimeSpan d) => (d < TimeSpan.Zero ? "−" : "+") + Duration(d.Duration());

    public static string BalanceClass(TimeSpan d) => d < TimeSpan.Zero ? "zw-negative" : d > TimeSpan.Zero ? "zw-positive" : "";
}
