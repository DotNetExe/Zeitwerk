namespace Zeitwerk.Web.Domain;

/// <summary>Ab wann das Überstundenkonto zählt und mit welchem Saldo es beginnt. Es gibt höchstens eine Zeile.</summary>
public class AccountStart
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }

    /// <summary>Saldo zu Beginn von <see cref="Date"/>, z. B. übernommene Überstunden; darf negativ sein.</summary>
    public TimeSpan Balance { get; set; }
}
