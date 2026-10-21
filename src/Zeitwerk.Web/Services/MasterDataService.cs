using Microsoft.EntityFrameworkCore;
using Zeitwerk.Web.Data;
using Zeitwerk.Web.Domain;

namespace Zeitwerk.Web.Services;

/// <summary>Kunden und Projekte verwalten.</summary>
public sealed class MasterDataService(IDbContextFactory<ZeitwerkDbContext> dbFactory, ChangeNotifier notifier)
{
    public async Task<List<Customer>> GetCustomersAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var customers = await db.Customers.AsNoTracking()
            .Include(c => c.Projects)
            .OrderBy(c => c.Name)
            .ToListAsync();
        foreach (var customer in customers)
            customer.Projects = customer.Projects.OrderBy(p => p.Name).ToList();
        return customers;
    }

    /// <summary>Projekte, auf die gebucht werden kann (Projekt und Kunde nicht archiviert).</summary>
    public async Task<List<Project>> GetActiveProjectsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Projects.AsNoTracking()
            .Include(p => p.Customer)
            .Where(p => !p.IsArchived && !p.Customer.IsArchived)
            .OrderBy(p => p.Customer.Name).ThenBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<string> SuggestColorAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var used = await db.Projects.Where(p => !p.IsArchived).Select(p => p.Color).ToListAsync();
        return ProjectColors.Suggest(used);
    }

    public async Task<string?> SaveCustomerAsync(int id, string name, string? ticketUrlTemplate, string? ticketPattern)
    {
        name = name.Trim();
        if (name.Length == 0)
            return "Bitte einen Namen angeben.";
        if (ValidateTicketLinks(ref ticketUrlTemplate, ref ticketPattern) is { } ticketError)
            return ticketError;

        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.Customers.AnyAsync(c => c.Id != id && c.Name == name))
            return "Ein Kunde mit diesem Namen existiert bereits.";

        var customer = id == 0 ? db.Customers.Add(new Customer()).Entity : await db.Customers.FindAsync(id);
        if (customer is null)
            return "Der Kunde existiert nicht mehr.";
        customer.Name = name;
        customer.TicketUrlTemplate = ticketUrlTemplate;
        customer.TicketPattern = ticketPattern;
        await db.SaveChangesAsync();
        notifier.NotifyChanged();
        return null;
    }

    public async Task<string?> SaveProjectAsync(int id, int customerId, string name, string color,
        string? ticketUrlTemplate, string? ticketPattern)
    {
        name = name.Trim();
        if (name.Length == 0)
            return "Bitte einen Namen angeben.";
        if (ValidateTicketLinks(ref ticketUrlTemplate, ref ticketPattern) is { } ticketError)
            return ticketError;

        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.Projects.AnyAsync(p => p.Id != id && p.CustomerId == customerId && p.Name == name))
            return "Dieser Kunde hat bereits ein Projekt mit diesem Namen.";

        var project = id == 0 ? db.Projects.Add(new Project()).Entity : await db.Projects.FindAsync(id);
        if (project is null)
            return "Das Projekt existiert nicht mehr.";
        project.CustomerId = customerId;
        project.Name = name;
        project.Color = color;
        project.TicketUrlTemplate = ticketUrlTemplate;
        project.TicketPattern = ticketPattern;
        await db.SaveChangesAsync();
        notifier.NotifyChanged();
        return null;
    }

    /// <summary>Leere Angaben werden zu <c>null</c> (= erben bzw. Standard); liefert eine Fehlermeldung bei ungültigen Angaben.</summary>
    private static string? ValidateTicketLinks(ref string? template, ref string? pattern)
    {
        template = string.IsNullOrWhiteSpace(template) ? null : template.Trim();
        pattern = string.IsNullOrWhiteSpace(pattern) ? null : pattern.Trim();
        return (template is null ? null : TicketLinks.ValidateTemplate(template))
            ?? (pattern is null ? null : TicketLinks.ValidatePattern(pattern));
    }

    public async Task SetCustomerArchivedAsync(int id, bool archived)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Customers.Where(c => c.Id == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.IsArchived, archived));
        notifier.NotifyChanged();
    }

    public async Task SetProjectArchivedAsync(int id, bool archived)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Projects.Where(p => p.Id == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsArchived, archived));
        notifier.NotifyChanged();
    }

    /// <summary>Löscht einen Kunden nur, wenn er keine Projekte hat.</summary>
    public async Task<string?> DeleteCustomerAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.Projects.AnyAsync(p => p.CustomerId == id))
            return "Der Kunde hat noch Projekte und kann nur archiviert werden.";
        await db.Customers.Where(c => c.Id == id).ExecuteDeleteAsync();
        notifier.NotifyChanged();
        return null;
    }

    /// <summary>Löscht ein Projekt nur, wenn keine Zeiten darauf gebucht sind.</summary>
    public async Task<string?> DeleteProjectAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.TimeEntries.AnyAsync(e => e.ProjectId == id))
            return "Auf das Projekt sind Zeiten gebucht; es kann nur archiviert werden.";
        await db.Projects.Where(p => p.Id == id).ExecuteDeleteAsync();
        notifier.NotifyChanged();
        return null;
    }
}
