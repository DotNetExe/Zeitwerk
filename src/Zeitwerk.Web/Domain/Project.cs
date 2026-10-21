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

    public string DisplayName => Customer is null ? Name : $"{Customer.Name} / {Name}";
}
