using Microsoft.EntityFrameworkCore;
using Zeitwerk.Web.Domain;

namespace Zeitwerk.Web.Data;

public class ZeitwerkDbContext(DbContextOptions<ZeitwerkDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(200);
            e.HasIndex(c => c.Name).IsUnique();
        });

        modelBuilder.Entity<Project>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(200);
            e.Property(p => p.Color).HasMaxLength(9);
            e.HasIndex(p => new { p.CustomerId, p.Name }).IsUnique();
            e.HasOne(p => p.Customer).WithMany(c => c.Projects).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(p => p.DisplayName);
        });

        modelBuilder.Entity<TimeEntry>(e =>
        {
            e.Property(t => t.Activity).HasMaxLength(500);
            e.HasOne(t => t.Project).WithMany().OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(t => t.Start);
            // Höchstens ein offener Zeiteintrag.
            e.HasIndex(t => t.End).IsUnique().AreNullsDistinct(false).HasFilter("\"End\" IS NULL")
                .HasDatabaseName("IX_TimeEntries_SingleOpen");
            e.Ignore(t => t.IsOpen);
        });
    }
}
