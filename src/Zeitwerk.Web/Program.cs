using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Zeitwerk.Web.Components;
using Zeitwerk.Web.Data;
using Zeitwerk.Web.Services;

var german = CultureInfo.GetCultureInfo("de-DE");
CultureInfo.DefaultThreadCurrentCulture = german;
CultureInfo.DefaultThreadCurrentUICulture = german;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

builder.Services.AddDbContextFactory<ZeitwerkDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.Configure<ZeitwerkOptions>(builder.Configuration.GetSection("Zeitwerk"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<Clock>();
builder.Services.AddSingleton<ChangeNotifier>();
builder.Services.AddScoped<TimerService>();
builder.Services.AddScoped<EntryService>();
builder.Services.AddScoped<MasterDataService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<OvertimeService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ZeitwerkDbContext>>().CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/export.csv", async (DateOnly from, DateOnly to, int? customerId, int? projectId, bool? perDay,
    IDbContextFactory<ZeitwerkDbContext> dbFactory, Clock clock) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var entries = await CsvExport.LoadAsync(db, clock, new ExportFilter(from, to, customerId, projectId));
    var daily = perDay == true;
    var csv = CsvExport.Encoding.GetBytes(daily ? CsvExport.BuildDaily(entries, clock.Zone) : CsvExport.Build(entries, clock.Zone));
    return Results.File(csv, "text/csv; charset=utf-8",
        $"zeitwerk_{from:yyyy-MM-dd}_{to:yyyy-MM-dd}{(daily ? "_tage" : "")}.csv");
});

app.Run();
