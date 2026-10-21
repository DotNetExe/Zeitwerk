namespace Zeitwerk.Web.Domain;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsArchived { get; set; }
    public List<Project> Projects { get; set; } = [];
}
