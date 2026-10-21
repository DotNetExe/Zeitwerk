using Microsoft.EntityFrameworkCore;
using Zeitwerk.Web.Domain;

namespace Zeitwerk.Web.Data;

public class ZeitwerkDbContext(DbContextOptions<ZeitwerkDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();
    public DbSet<Absence> Absences => Set<Absence>();
    public DbSet<AccountStart> AccountStarts => Set<AccountStart>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(200);
            e.Property(c => c.TicketUrlTemplate).HasMaxLength(500);
            e.Property(c => c.TicketPattern).HasMaxLength(200);
            e.HasIndex(c => c.Name).IsUnique();
        });

        modelBuilder.Entity<Project>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(200);
            e.Property(p => p.Color).HasMaxLength(9);
            e.Property(p => p.TicketUrlTemplate).HasMaxLength(500);
            e.Property(p => p.TicketPattern).HasMaxLength(200);
            e.HasIndex(p => new { p.CustomerId, p.Name }).IsUnique();
            e.HasOne(p => p.Customer).WithMany(c => c.Projects).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(p => p.DisplayName);
            e.Ignore(p => p.TicketLinks);
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

        modelBuilder.Entity<WorkSchedule>(e =>
        {
            e.HasIndex(w => w.ValidFrom).IsUnique();
            e.Ignore(w => w.WeekTotal);
        });

        modelBuilder.Entity<Absence>(e =>
        {
            e.Property(a => a.Kind).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(a => a.Date).IsUnique();
        });
    }
}
