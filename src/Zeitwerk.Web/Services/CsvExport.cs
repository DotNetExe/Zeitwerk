using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Zeitwerk.Web.Data;
using Zeitwerk.Web.Domain;

namespace Zeitwerk.Web.Services;

public sealed record ExportFilter(DateOnly From, DateOnly To, int? CustomerId, int? ProjectId);

/// <summary>CSV für deutsches Excel: Semikolon, Dezimalkomma, UTF-8 ohne BOM.</summary>
public static class CsvExport
{
    public static readonly Encoding Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    public static async Task<List<TimeEntry>> LoadAsync(ZeitwerkDbContext db, Clock clock, ExportFilter filter)
    {
        var from = clock.StartOfDay(filter.From);
        var to = clock.StartOfDay(filter.To.AddDays(1));
        var query = db.TimeEntries.AsNoTracking()
            .Include(e => e.Project).ThenInclude(p => p.Customer)
            .Where(e => e.End != null && e.Start >= from && e.Start < to);
        if (filter.CustomerId is { } customerId)
            query = query.Where(e => e.Project.CustomerId == customerId);
        if (filter.ProjectId is { } projectId)
            query = query.Where(e => e.ProjectId == projectId);
        return await query.OrderBy(e => e.Start).ToListAsync();
    }

    /// <summary>Nur abgeschlossene Einträge; Projekt und Kunde müssen geladen sein.</summary>
    public static string Build(IEnumerable<TimeEntry> entries, TimeZoneInfo zone)
    {
        var sb = new StringBuilder();
        sb.Append("Datum;Start;Ende;Dauer;Stunden;Kunde;Projekt;Tätigkeit\r\n");
        foreach (var entry in entries.Where(e => e.End is not null))
        {
            var start = TimeZoneInfo.ConvertTime(entry.Start, zone);
            var end = TimeZoneInfo.ConvertTime(entry.End!.Value, zone);
            var duration = entry.End.Value - entry.Start;
            string[] fields =
            [
                start.ToString("dd.MM.yyyy", German),
                start.ToString("HH:mm", German),
                end.ToString("HH:mm", German),
                FormatDuration(duration),
                duration.TotalHours.ToString("0.00", German),
                entry.Project.Customer.Name,
                entry.Project.Name,
                entry.Activity ?? "",
            ];
            sb.AppendJoin(';', fields.Select(Escape)).Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>Eine Zeile je Tag, Kunde und Projekt; Tätigkeiten in Reihenfolge ihres ersten Auftretens.</summary>
    public static string BuildDaily(IEnumerable<TimeEntry> entries, TimeZoneInfo zone)
    {
        var sb = new StringBuilder();
        sb.Append("Datum;Dauer;Stunden;Kunde;Projekt;Tätigkeiten\r\n");
        var days = entries
            .Where(e => e.End is not null)
            .OrderBy(e => e.Start)
            // nach Id, weil AsNoTracking je Eintrag eigene Projekt-Instanzen liefert
            .GroupBy(e => (Date: DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(e.Start, zone).DateTime), e.Project.Id))
            .Select(g => (g.Key.Date, g.First().Project, Entries: g.ToList()))
            .OrderBy(g => g.Date)
            .ThenBy(g => g.Project.Customer.Name, StringComparer.Create(German, ignoreCase: true))
            .ThenBy(g => g.Project.Name, StringComparer.Create(German, ignoreCase: true));
        foreach (var day in days)
        {
            var duration = day.Entries.Aggregate(TimeSpan.Zero, (acc, e) => acc + (e.End!.Value - e.Start));
            var activities = day.Entries.Select(e => e.Activity).OfType<string>().Where(a => a.Length > 0).Distinct();
            string[] fields =
            [
                day.Date.ToString("dd.MM.yyyy", German),
                FormatDuration(duration),
                duration.TotalHours.ToString("0.00", German),
                day.Project.Customer.Name,
                day.Project.Name,
                string.Join(", ", activities),
            ];
            sb.AppendJoin(';', fields.Select(Escape)).Append("\r\n");
        }
        return sb.ToString();
    }

    public static string FormatDuration(TimeSpan d) => $"{(int)d.TotalHours:00}:{d.Minutes:00}";

    private static string Escape(string field) =>
        field.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? $"\"{field.Replace("\"", "\"\"")}\"" : field;
}
