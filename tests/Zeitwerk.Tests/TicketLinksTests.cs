using Zeitwerk.Web.Domain;

namespace Zeitwerk.Tests;

public class TicketLinksTests
{
    private static readonly TicketLinkSettings Jira = new("https://jira.example.com/browse/{ticket}", TicketLinks.DefaultPattern);

    [Fact]
    public void Without_settings_the_activity_stays_plain_text()
    {
        Assert.Equal([new ActivitySegment("ABC-123 Review")], TicketLinks.Split("ABC-123 Review", null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Empty_activity_has_no_segments(string? activity)
    {
        Assert.Empty(TicketLinks.Split(activity, Jira));
    }

    [Fact]
    public void Ticket_numbers_become_links_and_the_rest_stays_text()
    {
        var segments = TicketLinks.Split("Review ABC-123 und XY2-7, fertig", Jira);

        Assert.Equal(
        [
            new ActivitySegment("Review "),
            new ActivitySegment("ABC-123", "https://jira.example.com/browse/ABC-123"),
            new ActivitySegment(" und "),
            new ActivitySegment("XY2-7", "https://jira.example.com/browse/XY2-7"),
            new ActivitySegment(", fertig"),
        ], segments);
    }

    [Fact]
    public void Activity_that_is_only_a_ticket_is_a_single_link()
    {
        Assert.Equal([new ActivitySegment("ABC-1", "https://jira.example.com/browse/ABC-1")], TicketLinks.Split("ABC-1", Jira));
    }

    [Theory]
    [InlineData("xABC-123")]
    [InlineData("ABC-123a")]
    [InlineData("abc-123")]
    [InlineData("A-1")]
    public void Ticket_numbers_must_stand_alone(string activity)
    {
        Assert.Equal([new ActivitySegment(activity)], TicketLinks.Split(activity, Jira));
    }

    [Fact]
    public void Custom_pattern_is_used()
    {
        var settings = new TicketLinkSettings("https://git.example.com/issues/{ticket}", @"#\d+");

        var segments = TicketLinks.Split("Fix #42", settings);

        Assert.Equal([new ActivitySegment("Fix "), new ActivitySegment("#42", "https://git.example.com/issues/%2342")], segments);
    }

    [Fact]
    public void Markup_in_the_activity_is_kept_as_raw_text_and_never_part_of_a_url()
    {
        var segments = TicketLinks.Split("<script>alert(1)</script> ABC-1", Jira);

        Assert.Equal(new ActivitySegment("<script>alert(1)</script> "), segments[0]);
        Assert.Equal("https://jira.example.com/browse/ABC-1", segments[1].Url);
    }

    [Fact]
    public void Matched_text_is_url_encoded_so_it_cannot_break_out_of_the_href()
    {
        // Ein großzügiges Muster, das Anführungszeichen und spitze Klammern mit erfasst.
        var settings = new TicketLinkSettings("https://jira.example.com/browse/{ticket}", @"T-[^ ]+");

        var segment = Assert.Single(TicketLinks.Split("T-1\"onclick=\"x()<b>", settings));

        Assert.Equal("T-1\"onclick=\"x()<b>", segment.Text);
        Assert.Equal("https://jira.example.com/browse/T-1%22onclick%3D%22x%28%29%3Cb%3E", segment.Url);
        Assert.DoesNotContain('"', segment.Url!);
        Assert.DoesNotContain('<', segment.Url!);
    }

    [Theory]
    [InlineData("javascript:alert('{ticket}')")]
    [InlineData("data:text/html,{ticket}")]
    [InlineData("/browse/{ticket}")]
    [InlineData("https://jira.example.com/browse/")]
    public void Invalid_templates_produce_no_links(string template)
    {
        var settings = new TicketLinkSettings(template, TicketLinks.DefaultPattern);

        Assert.NotNull(TicketLinks.ValidateTemplate(template));
        Assert.Equal([new ActivitySegment("ABC-1")], TicketLinks.Split("ABC-1", settings));
    }

    [Theory]
    [InlineData("https://jira.example.com/browse/{ticket}")]
    [InlineData("http://tracker.local/issue?id={ticket}&view=full")]
    public void Valid_templates_pass_validation(string template)
    {
        Assert.Null(TicketLinks.ValidateTemplate(template));
    }

    [Fact]
    public void Template_characters_are_normalized_in_the_url()
    {
        var settings = new TicketLinkSettings("https://jira.example.com/browse/{ticket}?a=\"b c\"", TicketLinks.DefaultPattern);

        var segment = Assert.Single(TicketLinks.Split("ABC-1", settings));

        Assert.Equal("https://jira.example.com/browse/ABC-1?a=%22b%20c%22", segment.Url);
    }

    [Fact]
    public void Invalid_pattern_is_rejected_and_produces_no_links()
    {
        var settings = new TicketLinkSettings(Jira.UrlTemplate, "[A-Z");

        Assert.NotNull(TicketLinks.ValidatePattern("[A-Z"));
        Assert.Equal([new ActivitySegment("ABC-1")], TicketLinks.Split("ABC-1", settings));
    }

    [Fact]
    public void Catastrophic_pattern_times_out_and_falls_back_to_text()
    {
        var settings = new TicketLinkSettings(Jira.UrlTemplate, "(a+)+$");
        var activity = new string('a', 40) + "!";

        Assert.Equal([new ActivitySegment(activity)], TicketLinks.Split(activity, settings));
    }

    [Fact]
    public void Empty_matches_are_ignored()
    {
        var settings = new TicketLinkSettings(Jira.UrlTemplate, @"\d*");

        Assert.Equal([new ActivitySegment("ab "), new ActivitySegment("12", "https://jira.example.com/browse/12")],
            TicketLinks.Split("ab 12", settings));
    }

    [Fact]
    public void Project_overrides_customer_settings()
    {
        var customer = new Customer { TicketUrlTemplate = "https://customer.example.com/{ticket}", TicketPattern = @"C-\d+" };
        var inherits = new Project { Customer = customer };
        var overridesUrl = new Project { Customer = customer, TicketUrlTemplate = "https://project.example.com/{ticket}" };
        var overridesBoth = new Project { Customer = customer, TicketUrlTemplate = "https://project.example.com/{ticket}", TicketPattern = @"P-\d+" };

        Assert.Equal(new TicketLinkSettings("https://customer.example.com/{ticket}", @"C-\d+"), inherits.TicketLinks);
        Assert.Equal(new TicketLinkSettings("https://project.example.com/{ticket}", @"C-\d+"), overridesUrl.TicketLinks);
        Assert.Equal(new TicketLinkSettings("https://project.example.com/{ticket}", @"P-\d+"), overridesBoth.TicketLinks);
    }

    [Fact]
    public void Without_any_template_there_are_no_settings_and_the_default_pattern_applies_otherwise()
    {
        Assert.Null(new Project { Customer = new Customer { TicketPattern = @"C-\d+" } }.TicketLinks);
        Assert.Equal(TicketLinks.DefaultPattern,
            new Project { Customer = new Customer { TicketUrlTemplate = "https://x.example.com/{ticket}" } }.TicketLinks!.Pattern);
    }
}
