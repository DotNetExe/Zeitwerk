namespace Zeitwerk.Web.Domain;

public enum AbsenceKind
{
    Vacation,
    Sick,
    Holiday,
}

/// <summary>Ein ganzer Tag ohne Soll-Arbeitszeit (Urlaub, Krankheit, Feiertag).</summary>
public class Absence
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public AbsenceKind Kind { get; set; }

    public static string Label(AbsenceKind kind) => kind switch
    {
        AbsenceKind.Vacation => "Urlaub",
        AbsenceKind.Sick => "Krankheit",
        _ => "Feiertag",
    };
}
