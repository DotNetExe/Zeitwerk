namespace Zeitwerk.Web.Domain;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsArchived { get; set; }

    /// <summary>URL-Vorlage für Ticket-Links mit Platzhalter <c>{ticket}</c>; <c>null</c> = keine Links.</summary>
    public string? TicketUrlTemplate { get; set; }

    /// <summary>Regulärer Ausdruck für Ticketnummern; <c>null</c> = <see cref="TicketLinks.DefaultPattern"/>.</summary>
    public string? TicketPattern { get; set; }

    public List<Project> Projects { get; set; } = [];
}
