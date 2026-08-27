using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Application.Dispatches.SendTripPickupEmail;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>
/// How <see cref="PickupEmailRenderer"/> resolves {{PickupTime}} / {{DropoffTime}}: a recipient's
/// own times win, and the trip-level times are the fallback. This is what lets a corridor run tell
/// the passenger boarding mid-route the time the vehicle reaches THEIR stop, rather than the time
/// it left the origin.
/// </summary>
public class PickupEmailRendererTests
{
    private const string TripPickup = "6:30 AM";
    private const string TripDropoff = "10:30 AM";

    private static RecipientInput Recipient(
        string email,
        string name,
        string? pickupStop = null,
        string? pickupTime = null,
        string? dropoffTime = null) =>
        new(email, name, pickupStop, PickupAddress: null, DropoffStop: null, DropoffStopAddress: null,
            pickupTime, dropoffTime);

    private static IReadOnlyList<OutgoingEmail> Render(params RecipientInput[] recipients) =>
        PickupEmailRenderer.Render(
            TestNotifications.CreateTemplate(),
            "Tuesday, August 4, 2026",
            TripPickup,
            TripDropoff,
            "Thompson – Lynn Lake",
            "NL-1042",
            clientName: null,
            recipients);

    [Fact]
    public void Each_recipient_gets_their_own_pickup_time()
    {
        var emails = Render(
            Recipient("alex@example.com", "Alex Moody", "Thompson", pickupTime: "6:30 AM"),
            Recipient("brie@example.com", "Brie Okonkwo", "Ponton", pickupTime: "8:05 AM"));

        Assert.Contains("6:30 AM", emails[0].HtmlBody);
        Assert.DoesNotContain("8:05 AM", emails[0].HtmlBody);

        // The whole point: the Ponton passenger is not told the Thompson departure time.
        Assert.Contains("8:05 AM", emails[1].HtmlBody);
        Assert.DoesNotContain("6:30 AM", emails[1].HtmlBody);
    }

    [Fact]
    public void A_recipient_without_times_falls_back_to_the_trip_level_ones()
    {
        var emails = Render(Recipient("alex@example.com", "Alex Moody", "Thompson"));

        Assert.Contains(TripPickup, emails[0].HtmlBody);
        Assert.Contains(TripDropoff, emails[0].HtmlBody);
    }

    [Fact]
    public void A_blank_recipient_time_falls_back_rather_than_rendering_an_empty_hole()
    {
        var emails = Render(Recipient(
            "alex@example.com", "Alex Moody", "Thompson", pickupTime: "   ", dropoffTime: ""));

        Assert.Contains(TripPickup, emails[0].HtmlBody);
        Assert.Contains(TripDropoff, emails[0].HtmlBody);
    }

    [Fact]
    public void The_dropoff_time_is_resolved_per_recipient_too()
    {
        var emails = Render(
            Recipient("alex@example.com", "Alex Moody", "Thompson", "6:30 AM", "8:05 AM"),
            Recipient("brie@example.com", "Brie Okonkwo", "Thompson", "6:30 AM", "10:30 AM"));

        Assert.Contains("8:05 AM", emails[0].HtmlBody);
        Assert.Contains("10:30 AM", emails[1].HtmlBody);
    }

    [Fact]
    public void The_plain_text_fallback_carries_the_same_resolved_time()
    {
        var emails = Render(Recipient("brie@example.com", "Brie Okonkwo", "Ponton", pickupTime: "8:05 AM"));

        Assert.Contains("8:05 AM", emails[0].TextBody);
        Assert.DoesNotContain(TripPickup, emails[0].TextBody);
    }
}
