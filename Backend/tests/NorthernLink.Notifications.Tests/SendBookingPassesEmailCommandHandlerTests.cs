using NorthernLink.Notifications.Application.Dispatches;
using NorthernLink.Notifications.Application.Dispatches.SendBookingPassesEmail;
using NorthernLink.Notifications.Domain;
using NorthernLink.Notifications.Domain.Dispatches;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>The booking-passes send handler's gates, composition, idempotent replay, and persistence.</summary>
public class SendBookingPassesEmailCommandHandlerTests
{
    private static readonly Guid BookingId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly InMemoryEmailDispatchRepository _dispatches = new();
    private readonly FakeEmailSender _sender = new();

    private SendBookingPassesEmailCommandHandler Handler() => new(_dispatches, _sender);

    private static SendBookingPassesEmailCommand Command(
        Guid? dispatchId = null,
        Guid? bookingId = null,
        string bookingReference = "NL-7K3M2Q",
        BookingPassSheet? sheet = null,
        IReadOnlyList<PassRecipientInput>? recipients = null) => new(
        TestNotifications.TenantId,
        dispatchId ?? Guid.NewGuid(),
        bookingId ?? BookingId,
        bookingReference,
        sheet ?? TestNotifications.SampleBookingPassSheet(),
        recipients ?? [new PassRecipientInput("doris@example.com", "Doris Spence")]);

    [Fact]
    public async Task Missing_booking_id_is_rejected_and_nothing_is_sent()
    {
        var result = await Handler().Handle(Command(bookingId: Guid.Empty), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.BookingRequired, result.Error);
        Assert.Empty(_sender.Batches);
        Assert.Empty(_dispatches.Dispatches);
    }

    [Fact]
    public async Task Missing_booking_reference_is_rejected_and_nothing_is_sent()
    {
        var result = await Handler().Handle(Command(bookingReference: "  "), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.BookingRequired, result.Error);
        Assert.Empty(_sender.Batches);
    }

    [Fact]
    public async Task Sheet_with_no_travellers_is_rejected_before_sending()
    {
        var result = await Handler().Handle(
            Command(sheet: TestNotifications.SampleBookingPassSheet(travellers: [])),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.NoTravellers, result.Error);
        Assert.Empty(_sender.Batches);
    }

    [Fact]
    public async Task Empty_recipient_list_is_rejected()
    {
        var result = await Handler().Handle(Command(recipients: []), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.NoRecipients, result.Error);
        Assert.Empty(_sender.Batches);
    }

    [Fact]
    public async Task Invalid_recipient_email_is_rejected_before_sending()
    {
        var result = await Handler().Handle(
            Command(recipients: [new PassRecipientInput("867-5309", "Jenny")]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Notifications.Dispatch.InvalidRecipientEmail", result.Error.Code);
        Assert.Empty(_sender.Batches);
    }

    [Fact]
    public async Task More_than_the_distinct_recipient_cap_is_rejected()
    {
        var recipients = Enumerable.Range(0, EmailDispatch.MaxRecipients + 1)
            .Select(i => new PassRecipientInput($"contact{i}@example.com", $"Contact {i}"))
            .ToList();

        var result = await Handler().Handle(Command(recipients: recipients), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.TooManyRecipients, result.Error);
        Assert.Empty(_sender.Batches);
    }

    [Fact]
    public async Task Duplicate_addresses_are_sent_and_recorded_once()
    {
        var result = await Handler().Handle(
            Command(recipients:
            [
                new PassRecipientInput("doris@example.com", "Doris Spence"),
                new PassRecipientInput("DORIS@example.com", "D. Spence"),
                new PassRecipientInput("  doris@example.com  ", "Doris"),
            ]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var email = Assert.Single(Assert.Single(_sender.Batches));
        Assert.Equal("doris@example.com", email.To);
        var recipient = Assert.Single(result.Value.Recipients);
        Assert.Equal("Doris Spence", recipient.PassengerName); // first occurrence's name wins
    }

    [Fact]
    public async Task Replayed_DispatchId_returns_the_stored_dispatch_without_resending()
    {
        var dispatchId = Guid.NewGuid();

        var first = await Handler().Handle(Command(dispatchId), CancellationToken.None);
        Assert.True(first.IsSuccess);
        Assert.Single(_sender.Batches);

        var replay = await Handler().Handle(Command(dispatchId), CancellationToken.None);

        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Value.Id, replay.Value.Id);
        Assert.Equal(first.Value.SentAtUtc, replay.Value.SentAtUtc);
        Assert.Single(_sender.Batches); // no second provider call
        Assert.Single(_dispatches.Dispatches);
    }

    [Fact]
    public async Task Happy_path_sends_html_passes_persists_and_returns_outcomes()
    {
        var command = Command(recipients:
        [
            new PassRecipientInput("doris@example.com", "Doris Spence"),
            new PassRecipientInput("dispatch@northernlink.example", "Dispatch copy"),
        ]);

        var result = await Handler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Equal(command.DispatchId, response.Id);
        Assert.Equal("Sent", response.Status);
        Assert.Equal(2, response.Recipients.Count);
        Assert.Equal("CommunityBookingPasses", response.ServiceType);
        Assert.Equal(BookingId, response.BookingId);
        Assert.Equal("NL-7K3M2Q", response.BookingReference);

        // One batch, the identical email to every recipient, no attachment.
        var batch = Assert.Single(_sender.Batches);
        Assert.Equal(2, batch.Count);
        foreach (var email in batch)
        {
            Assert.Equal("Your Northern Link passes — NL-7K3M2Q — Thompson ↔ Lynn Lake — Tuesday, September 15, 2026", email.Subject);
            Assert.Contains("Doris Spence", email.HtmlBody);
            Assert.Contains("Sam Spence", email.HtmlBody);
            Assert.Contains("2 of 2", email.HtmlBody);
            Assert.Null(email.Attachments);
            Assert.False(string.IsNullOrWhiteSpace(email.TextBody));
        }

        // Recorded as a booking-anchored dispatch: no trip, no template, no client.
        Assert.Equal(1, _dispatches.SaveChangesCallCount);
        var stored = Assert.Single(_dispatches.Dispatches);
        Assert.Equal(BookingId, stored.BookingId);
        Assert.Equal("NL-7K3M2Q", stored.BookingReference);
        Assert.Equal(NotificationServiceType.CommunityBookingPasses, stored.ServiceType);
        Assert.Null(stored.TripId);
        Assert.Null(stored.TripNumber);
        Assert.Null(stored.ManifestId);
        Assert.Null(stored.TemplateId);
        Assert.Null(stored.TemplateName);
        Assert.Null(stored.ClientId);
        Assert.Null(stored.ClientName);
        Assert.Equal(EmailDispatchStatus.Sent, stored.Status);
    }

    [Fact]
    public async Task Total_provider_failure_still_persists_and_returns_success_with_failed_outcomes()
    {
        _sender.OutcomeFor = (_, _) => new(false, "Postmark.Unauthorized", "Bad token.", null);

        var result = await Handler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess); // outcomes are data, not an error path
        Assert.Equal("Failed", result.Value.Status);
        var recipient = Assert.Single(result.Value.Recipients);
        Assert.Equal("Postmark.Unauthorized", recipient.ErrorCode);
        Assert.Single(_dispatches.Dispatches);
    }
}
