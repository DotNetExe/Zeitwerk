namespace Zeitwerk.Web.Domain;

/// <summary>Soll-Arbeitszeit je Wochentag, gültig ab einem Datum bis zur nächsten Version.</summary>
public class WorkSchedule
{
    public int Id { get; set; }
    public DateOnly ValidFrom { get; set; }

    public TimeSpan Monday { get; set; } = TimeSpan.FromHours(8);
    public TimeSpan Tuesday { get; set; } = TimeSpan.FromHours(8);
    public TimeSpan Wednesday { get; set; } = TimeSpan.FromHours(8);
    public TimeSpan Thursday { get; set; } = TimeSpan.FromHours(8);
    public TimeSpan Friday { get; set; } = TimeSpan.FromHours(8);
    public TimeSpan Saturday { get; set; }
    public TimeSpan Sunday { get; set; }

    /// <summary>Standard: Mo–Fr je 8 h, gilt seit jeher.</summary>
    public static WorkSchedule Default => new() { ValidFrom = DateOnly.MinValue };

    public TimeSpan this[DayOfWeek day]
    {
        get => day switch
        {
            DayOfWeek.Monday => Monday,
            DayOfWeek.Tuesday => Tuesday,
            DayOfWeek.Wednesday => Wednesday,
            DayOfWeek.Thursday => Thursday,
            DayOfWeek.Friday => Friday,
            DayOfWeek.Saturday => Saturday,
            _ => Sunday,
        };
        set
        {
            switch (day)
            {
                case DayOfWeek.Monday: Monday = value; break;
                case DayOfWeek.Tuesday: Tuesday = value; break;
                case DayOfWeek.Wednesday: Wednesday = value; break;
                case DayOfWeek.Thursday: Thursday = value; break;
                case DayOfWeek.Friday: Friday = value; break;
                case DayOfWeek.Saturday: Saturday = value; break;
                default: Sunday = value; break;
            }
        }
    }

    public TimeSpan WeekTotal => Monday + Tuesday + Wednesday + Thursday + Friday + Saturday + Sunday;
}
