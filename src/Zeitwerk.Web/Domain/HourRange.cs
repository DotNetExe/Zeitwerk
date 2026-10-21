namespace Zeitwerk.Web.Domain;

/// <summary>Sichtbarer Ausschnitt einer Zeitleiste in vollen Stunden [<see cref="From"/>, <see cref="To"/>].</summary>
public readonly record struct HourRange(int From, int To)
{
    /// <summary>Standard-Ausschnitt 07–19 Uhr.</summary>
    public static readonly HourRange Default = new(7, 19);

    /// <summary>Erweitert den Ausschnitt auf volle Stunden um die Minutenspanne (seit Tagesbeginn), höchstens bis 24 Uhr.</summary>
    public HourRange Include(int startMinute, int endMinute) =>
        new(Math.Max(0, Math.Min(From, startMinute / 60)), Math.Min(24, Math.Max(To, (endMinute + 59) / 60)));

    public HourRange Union(HourRange other) => new(Math.Min(From, other.From), Math.Max(To, other.To));
}
