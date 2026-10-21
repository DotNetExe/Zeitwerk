using Zeitwerk.Web.Components.Shared;

namespace Zeitwerk.Tests;

public class FuzzyMatchTests
{
    [Fact]
    public void Empty_query_matches_everything() =>
        Assert.Equal(0, FuzzyMatch.Score("  ", "Acme / Website"));

    [Theory]
    [InlineData("web", "Acme / Website")]
    [InlineData("WEB", "Acme / Website")]
    [InlineData("wbs", "Acme / Website")]
    [InlineData("acme web", "Acme / Website")]
    [InlineData("übe", "Müller / Überarbeitung")]
    public void Matches_substrings_and_subsequences(string query, string text) =>
        Assert.NotNull(FuzzyMatch.Score(query, text));

    [Theory]
    [InlineData("xyz", "Acme / Website")]
    [InlineData("etisbew", "Acme / Website")]
    [InlineData("acme shop", "Acme / Website")]
    public void Rejects_missing_letters_wrong_order_and_missing_terms(string query, string text) =>
        Assert.Null(FuzzyMatch.Score(query, text));

    [Fact]
    public void Substring_beats_subsequence() =>
        Assert.True(FuzzyMatch.Score("web", "Acme / Website") > FuzzyMatch.Score("web", "Acme / Wartung Ebay Backend"));

    [Fact]
    public void Word_start_beats_inner_match() =>
        Assert.True(FuzzyMatch.Score("ex", "Gehe zu Export") > FuzzyMatch.Score("ex", "Acme / Textil"));

    [Fact]
    public void Filter_keeps_order_without_query_and_sorts_by_score_otherwise()
    {
        PaletteItem Item(string title) => new("Projekt", title, null, "", null, () => Task.CompletedTask);
        var items = new[] { Item("Acme / Wartung Ebay Backend"), Item("Gehe zu Export"), Item("Acme / Website") };

        Assert.Equal(items, PaletteItem.Filter(items, null, 10));
        Assert.Equal(["Acme / Website", "Acme / Wartung Ebay Backend"],
            PaletteItem.Filter(items, "web", 10).Select(i => i.Title));
        Assert.Single(PaletteItem.Filter(items, "", 1));
    }

    [Fact]
    public void Filter_searches_subtitle_and_keywords()
    {
        var items = new[]
        {
            new PaletteItem("Zuletzt", "Acme / Website", "TICKET-42", "", null, () => Task.CompletedTask),
            new PaletteItem("Befehl", "Gehe zu Export", null, "", null, () => Task.CompletedTask) { Keywords = "csv" },
        };

        Assert.Equal("Acme / Website", Assert.Single(PaletteItem.Filter(items, "ticket 42", 10)).Title);
        Assert.Equal("Gehe zu Export", Assert.Single(PaletteItem.Filter(items, "csv", 10)).Title);
    }
}
