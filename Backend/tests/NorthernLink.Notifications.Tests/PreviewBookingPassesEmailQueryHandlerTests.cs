using NorthernLink.Notifications.Application.Dispatches;
using NorthernLink.Notifications.Application.Dispatches.PreviewBookingPassesEmail;
using NorthernLink.Notifications.Application.Dispatches.SendBookingPassesEmail;
using NorthernLink.Notifications.Domain.Dispatches;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>
/// The booking-passes preview handler: same gates as the send (shared recipient gate +
/// booking anchor check), same composition (shared composer), nothing sent — the handler
/// takes no <c>IEmailSender</c> at all, so sending is structurally impossible.
/// </summary>
public class PreviewBookingPassesEmailQueryHandlerTests
{
    private static readonly Guid BookingId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static PreviewBookingPassesEmailQueryHandler Handler() => new();

    private static PreviewBookingPassesEmailQuery Query(
        Guid? bookingId = null,
        string bookingReference = "NL-7K3M2Q",
        BookingPassSheet? sheet = null,
        IReadOnlyList<PassRecipientInput>? recipients = null) => new(
        TestNotifications.TenantId,
        bookingId ?? BookingId,
        bookingReference,
        sheet ?? TestNotifications.SampleBookingPassSheet(),
        recipients ?? [new PassRecipientInput("doris@example.com", "Doris Spence")]);

    [Fact]
    public async Task Missing_booking_id_fails_exactly_like_the_send()
    {
        var result = await Handler().Handle(Query(bookingId: Guid.Empty), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.BookingRequired, result.Error);
    }

    [Fact]
    public async Task Missing_booking_reference_fails_exactly_like_the_send()
    {
        var result = await Handler().Handle(Query(bookingReference: ""), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.BookingRequired, result.Error);
    }

    [Fact]
    public async Task No_travellers_fails_exactly_like_the_send()
    {
        var result = await Handler().Handle(
            Query(sheet: TestNotifications.SampleBookingPassSheet(travellers: [])), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.NoTravellers, result.Error);
    }

    [Fact]
    public async Task Empty_recipient_list_fails_exactly_like_the_send()
    {
        var result = await Handler().Handle(Query(recipients: []), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.NoRecipients, result.Error);
    }

    [Fact]
    public async Task Invalid_recipient_email_fails_exactly_like_the_send()
    {
        var result = await Handler().Handle(
            Query(recipients: [new PassRecipientInput("867-5309", "Jenny")]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Notifications.Dispatch.InvalidRecipientEmail", result.Error.Code);
    }

    [Fact]
    public async Task More_than_the_distinct_recipient_cap_fails_exactly_like_the_send()
    {
        var recipients = Enumerable.Range(0, EmailDispatch.MaxRecipients + 1)
            .Select(i => new PassRecipientInput($"contact{i}@example.com", $"Contact {i}"))
            .ToList();

        var result = await Handler().Handle(Query(recipients: recipients), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.TooManyRecipients, result.Error);
    }

    [Fact]
    public async Task Preview_composes_subject_and_bodies_without_sending()
    {
        var result = await Handler().Handle(Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Equal("Your Northern Link passes — NL-7K3M2Q — Thompson ↔ Lynn Lake — Tuesday, September 15, 2026", response.Subject);
        Assert.Contains("Doris Spence", response.HtmlBody);
        Assert.Contains("Sam Spence", response.HtmlBody);
        Assert.Contains("1 of 2", response.HtmlBody);
        Assert.Contains("2 of 2", response.HtmlBody);
        Assert.DoesNotContain("<p>", response.TextBody);
        Assert.Contains("NL-7K3M2Q", response.TextBody);
    }

    [Fact]
    public async Task Recipients_are_deduplicated_and_echoed_for_display()
    {
        var result = await Handler().Handle(
            Query(recipients:
            [
                new PassRecipientInput("doris@example.com", "Doris Spence"),
                new PassRecipientInput("DORIS@example.com", "D. Spence"),
                new PassRecipientInput("dispatch@northernlink.example", "Dispatch copy"),
            ]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.RecipientCount);
        Assert.Equal(new[] { "doris@example.com", "dispatch@northernlink.example" }, result.Value.Recipients.ToArray());
    }
}
