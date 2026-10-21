namespace Zeitwerk.Web.Domain;

public class TimeEntry
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// <summary>Freitext, meist eine Ticketnummer. <c>null</c> statt Leerstring.</summary>
    public string? Activity { get; set; }

    /// <summary>Beginn in UTC, liegt immer auf dem Raster.</summary>
    public DateTimeOffset Start { get; set; }

    /// <summary>Ende in UTC, liegt immer auf dem Raster; <c>null</c> = offener Eintrag (laufend oder on hold).</summary>
    public DateTimeOffset? End { get; set; }

    public bool IsOpen => End is null;

    public bool IsOnHold(DateTimeOffset now) => IsOpen && Start > now;

    public bool IsSameWork(int projectId, string? activity) =>
        ProjectId == projectId && Activity == NormalizeActivity(activity);

    /// <summary>Dauer bis <paramref name="now"/> bei offenen Einträgen; nie negativ.</summary>
    public TimeSpan Duration(DateTimeOffset now)
    {
        var end = End ?? now;
        return end > Start ? end - Start : TimeSpan.Zero;
    }

    public static string? NormalizeActivity(string? activity) =>
        string.IsNullOrWhiteSpace(activity) ? null : activity.Trim();
}
