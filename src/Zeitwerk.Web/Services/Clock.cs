using Microsoft.Extensions.Options;
using Zeitwerk.Web.Domain;

namespace Zeitwerk.Web.Services;

public sealed class ZeitwerkOptions
{
    public string TimeZone { get; set; } = "Europe/Berlin";
    public int ToleranceSeconds { get; set; } = 60;
}

/// <summary>Aktuelle Zeit und Umrechnung zwischen UTC und der Anzeige-Zeitzone.</summary>
public sealed class Clock(TimeProvider time, IOptions<ZeitwerkOptions> options)
{
    public TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public TimerSettings TimerSettings => new(TimeSpan.FromSeconds(options.Value.ToleranceSeconds), Zone);

    public DateTimeOffset UtcNow => time.GetUtcNow();

    public DateOnly Today => LocalDate(UtcNow);

    public DateTime ToLocal(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, Zone).DateTime;

    public DateOnly LocalDate(DateTimeOffset utc) => DateOnly.FromDateTime(ToLocal(utc));

    public DateTimeOffset ToUtc(DateTime local) =>
        new(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone), TimeSpan.Zero);

    public DateTimeOffset StartOfDay(DateOnly date) => DaySplitter.StartOfDay(date, Zone);
}
