using Zeitwerk.Web.Domain;

namespace Zeitwerk.Web.Services;

/// <summary>Lädt die Zeiteinträge für die Auswertung samt Vorzeitraum.</summary>
public sealed class ReportService(EntryService entries, Clock clock)
{
    public async Task<Report> GetReportAsync(ReportPeriod period)
    {
        var loaded = await entries.GetEntriesAsync(period.Previous().From, period.ToExclusive);
        return ReportBuilder.Build(loaded, period, clock.Zone);
    }
}
