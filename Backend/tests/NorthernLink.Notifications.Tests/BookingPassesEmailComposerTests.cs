using System.Text.RegularExpressions;
using NorthernLink.Notifications.Application.Dispatches;
using NorthernLink.Notifications.Application.Dispatches.SendBookingPassesEmail;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>
/// The booking-passes composer: one bordered pass block per traveller, every sheet field
/// HTML-encoded (traveller names are customer input), and a tag-free text fallback.
/// </summary>
public class BookingPassesEmailComposerTests
{
    private static int PassBlockCount(string html) =>
        Regex.Matches(html, Regex.Escape($"class=\"{BookingPassesEmailComposer.PassBlockClass}\"")).Count;

    [Fact]
    public void Subject_carries_reference_corridor_and_date()
    {
        var composition = BookingPassesEmailComposer.Compose(TestNotifications.SampleBookingPassSheet());

        Assert.Equal(
            "Your Northern Link passes — NL-7K3M2Q — Thompson ↔ Lynn Lake — Tuesday, September 15, 2026",
            composition.Subject);
    }

    [Fact]
    public void One_pass_block_per_traveller_with_name_and_seat()
    {
        var sheet = TestNotifications.SampleBookingPassSheet(travellers:
        [
            new BookingPassTraveller("Doris Spence", "204-555-0199", "1 of 3"),
            new BookingPassTraveller("Sam Spence", null, "2 of 3"),
            new BookingPassTraveller("Lee Spence", null, "3 of 3"),
        ]);

        var composition = BookingPassesEmailComposer.Compose(sheet);

        Assert.Equal(3, PassBlockCount(composition.HtmlBody));
        Assert.Contains("Doris Spence", composition.HtmlBody);
        Assert.Contains("Sam Spence", composition.HtmlBody);
        Assert.Contains("Lee Spence", composition.HtmlBody);
        Assert.Contains("Boarding pass 1 of 3", composition.HtmlBody);
        Assert.Contains("Boarding pass 3 of 3", composition.HtmlBody);
        Assert.Contains("204-555-0199", composition.HtmlBody);
        Assert.Equal(3, Regex.Matches(composition.HtmlBody, "Present this pass at boarding").Count);
    }

    [Fact]
    public void Single_traveller_gets_exactly_one_block()
    {
        var sheet = TestNotifications.SampleBookingPassSheet(travellers:
            [new BookingPassTraveller("Doris Spence", null, "1 of 1")]);

        var composition = BookingPassesEmailComposer.Compose(sheet);

        Assert.Equal(1, PassBlockCount(composition.HtmlBody));
        Assert.Contains("your 1 boarding pass for booking", composition.HtmlBody);
    }

    [Fact]
    public void Booking_summary_and_notes_are_rendered()
    {
        var composition = BookingPassesEmailComposer.Compose(TestNotifications.SampleBookingPassSheet());

        Assert.Contains("Thompson Depot", composition.HtmlBody);
        Assert.Contains("Lynn Lake Terminal", composition.HtmlBody);
        Assert.Contains("E-Transfer", composition.HtmlBody);
        Assert.Contains("Unpaid", composition.HtmlBody);
        Assert.Contains("Wheelchair-accessible seating requested.", composition.HtmlBody);
    }

    [Fact]
    public void Absent_notes_render_no_notes_line()
    {
        var composition = BookingPassesEmailComposer.Compose(TestNotifications.SampleBookingPassSheet(notes: null));

        Assert.DoesNotContain("Notes:", composition.HtmlBody);
    }

    [Fact]
    public void Customer_input_is_html_encoded()
    {
        var sheet = TestNotifications.SampleBookingPassSheet(
            customerName: "Doris <b>Spence</b>",
            notes: "Bring \"ID\" & ticket",
            travellers: [new BookingPassTraveller("<script>alert(1)</script>", null, "1 of 1")]);

        var composition = BookingPassesEmailComposer.Compose(sheet);

        Assert.DoesNotContain("<script>", composition.HtmlBody);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", composition.HtmlBody);
        Assert.DoesNotContain("<b>Spence</b>", composition.HtmlBody);
        Assert.Contains("&lt;b&gt;Spence&lt;/b&gt;", composition.HtmlBody);
        Assert.Contains("&quot;ID&quot; &amp; ticket", composition.HtmlBody);
    }

    [Fact]
    public void Text_body_has_no_tags_and_keeps_the_content()
    {
        var composition = BookingPassesEmailComposer.Compose(TestNotifications.SampleBookingPassSheet());

        Assert.DoesNotContain("<p>", composition.TextBody);
        Assert.DoesNotContain("<div", composition.TextBody);
        Assert.DoesNotContain("<table", composition.TextBody);
        Assert.Contains("NL-7K3M2Q", composition.TextBody);
        Assert.Contains("Doris Spence", composition.TextBody);
        Assert.Contains("Sam Spence", composition.TextBody);
    }
}
