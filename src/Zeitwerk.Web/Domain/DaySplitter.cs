namespace Zeitwerk.Web.Domain;

/// <summary>Teilt abgeschlossene Zeiteinträge an lokalen Mitternächten (R14).</summary>
public static class DaySplitter
{
    /// <summary>Kürzt <paramref name="entry"/> auf seinen ersten Tag und liefert die Folgestücke.</summary>
    public static List<TimeEntry> Split(TimeEntry entry, TimeZoneInfo zone)
    {
        var pieces = new List<TimeEntry>();
        if (entry.End is not { } end)
            return pieces;

        var midnight = NextMidnight(entry.Start, zone);
        if (midnight >= end)
            return pieces;

        entry.End = midnight;
        while (midnight < end)
        {
            var next = NextMidnight(midnight, zone);
            pieces.Add(new TimeEntry
            {
                ProjectId = entry.ProjectId,
                Activity = entry.Activity,
                Start = midnight,
                End = next < end ? next : end,
            });
            midnight = next;
        }
        return pieces;
    }

    /// <summary>Beginn des lokalen Tages, der auf den Tag von <paramref name="t"/> folgt, in UTC.</summary>
    public static DateTimeOffset NextMidnight(DateTimeOffset t, TimeZoneInfo zone)
    {
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(t, zone).DateTime);
        return StartOfDay(localDate.AddDays(1), zone);
    }

    public static DateTimeOffset StartOfDay(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }
}
