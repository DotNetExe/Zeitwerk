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
    public void Encoding_has_no_byte_order_mark() =>
        Assert.Empty(CsvExport.Encoding.GetPreamble());
}
