namespace Zeitwerk.Web.Domain;

public class Project
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string Name { get; set; } = "";

    /// <summary>Hex-Farbe, z. B. <c>#4FC3F7</c>.</summary>
    public string Color { get; set; } = ProjectColors.Palette[0];

    public bool IsArchived { get; set; }

    /// <summary>Überschreibt die Ticket-URL des Kunden; <c>null</c> = vom Kunden übernehmen.</summary>
    public string? TicketUrlTemplate { get; set; }

    /// <summary>Überschreibt das Ticket-Muster des Kunden; <c>null</c> = vom Kunden übernehmen.</summary>
    public string? TicketPattern { get; set; }

    public string DisplayName => Customer is null ? Name : $"{Customer.Name} / {Name}";

    /// <summary>Wirksame Ticket-Link-Konfiguration (Projekt vor Kunde); <c>null</c>, wenn keine URL-Vorlage gesetzt ist.</summary>
    public TicketLinkSettings? TicketLinks =>
        (TicketUrlTemplate ?? Customer?.TicketUrlTemplate) is { } template
            ? new(template, TicketPattern ?? Customer?.TicketPattern ?? Domain.TicketLinks.DefaultPattern)
            : null;
}
