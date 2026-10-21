using Zeitwerk.Web.Domain;
using Zeitwerk.Web.Services;
using static Zeitwerk.Tests.TimerHarness;

namespace Zeitwerk.Tests;

public class CsvExportTests
{
    private static readonly Project Project = new()
    {
        Id = A, Name = "Webshop", Customer = new Customer { Name = "Müller; Söhne" },
    };

    [Fact]
    public void Exports_closed_entries_in_german_excel_format()
    {
        TimeEntry[] entries =
        [
            new() { Project = Project, ProjectId = A, Activity = "SHOP-42", Start = T("09:00"), End = T("10:45") },
            new() { Project = Project, ProjectId = A, Start = T("11:00") }, // offen, wird ausgelassen
        ];

        var csv = CsvExport.Build(entries, TimeZoneInfo.Utc);

        Assert.Equal(
            "Datum;Start;Ende;Dauer;Stunden;Kunde;Projekt;Tätigkeit\r\n" +
            "21.10.2026;09:00;10:45;01:45;1,75;\"Müller; Söhne\";Webshop;SHOP-42\r\n",
            csv);
    }

    [Fact]
    public void Times_are_converted_to_display_time_zone()
    {
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        TimeEntry[] entries = [new() { Project = Project, Start = T("07:00"), End = T("08:00") }];

        var line = CsvExport.Build(entries, berlin).Split("\r\n")[1];

        Assert.StartsWith("21.10.2026;09:00;10:00;", line);
    }

    [Fact]
    public void Daily_export_sums_per_day_and_project()
    {
        var other = new Project { Id = B, Name = "App", Customer = new Customer { Name = "Alpha" } };
        TimeEntry[] entries =
        [
            new() { Project = Project, Activity = "SHOP-42", Start = T("09:00"), End = T("10:45") },
            new() { Project = other, Start = T("10:45"), End = T("11:00") },
            new() { Project = Project, Activity = "SHOP-7", Start = T("11:00"), End = T("11:30") },
            new() { Project = Project, Activity = "SHOP-42", Start = T("13:00"), End = T("14:00") },
            new() { Project = Project, Start = T("22.10 08:00"), End = T("22.10 08:30") },
            new() { Project = Project, Start = T("22.10 09:00") }, // offen, wird ausgelassen
        ];

        var csv = CsvExport.BuildDaily(entries, TimeZoneInfo.Utc);

        Assert.Equal(
            "Datum;Dauer;Stunden;Kunde;Projekt;Tätigkeiten\r\n" +
            "21.10.2026;00:15;0,25;Alpha;App;\r\n" +
            "21.10.2026;03:15;3,25;\"Müller; Söhne\";Webshop;SHOP-42, SHOP-7\r\n" +
            "22.10.2026;00:30;0,50;\"Müller; Söhne\";Webshop;\r\n",
            csv);
    }

    [Fact]
    public void Daily_export_groups_by_local_date()
    {
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        // 22:30–23:00 UTC ist am 22.10. lokal 00:30–01:00
        TimeEntry[] entries = [new() { Project = Project, Start = T("22:30"), End = T("23:00") }];

        var line = CsvExport.BuildDaily(entries, berlin).Split("\r\n")[1];

        Assert.StartsWith("22.10.2026;00:30;", line);
    }

    [Fact]
    public void Encoding_has_no_byte_order_mark() =>
        Assert.Empty(CsvExport.Encoding.GetPreamble());
}
