using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Zeitwerk.Web.Data;

/// <summary>Für <c>dotnet ef</c>: erzeugt Migrationen ohne die App (und deren Auto-Migration) zu starten.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ZeitwerkDbContext>
{
    public ZeitwerkDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ZeitwerkDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=zeitwerk;Username=zeitwerk;Password=zeitwerk") // docker-compose.dev.yml
            .Options);
}
