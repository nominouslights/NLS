using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Shared.IntegrationEvents.Booking;
using NorthernLink.Notifications.Application.Integration;
using NorthernLink.Notifications.Domain;
using NorthernLink.Notifications.Domain.Dispatches;
using NorthernLink.Notifications.Domain.Templates;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>
/// The module's first event-driven send: chunking into ≤16-recipient dispatches,
/// deterministic per-chunk dispatch ids that make redelivery a no-op, the built-in body,
/// and the CommunityBookingAtRisk template override.
/// </summary>
public class BookingDayRevertedIntegrationEventHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BookingDayId = Guid.NewGuid();

    private readonly InMemoryEmailTemplateRepository _templates = new();
    private readonly InMemoryEmailDispatchRepository _dispatches = new();
    private readonly FakeEmailSender _sender = new();

    private BookingDayRevertedIntegrationEventHandler Handler => new(
        _templates,
        _dispatches,
        _sender,
        NullLogger<BookingDayRevertedIntegrationEventHandler>.Instance);

    private static BookingDayRevertedIntegrationEvent Event(
        int recipientCount = 2,
        Guid? tripId = null,
        string? tripNumber = null,
        Guid? eventId = null)
    {
        var recipients = Enumerable.Range(1, recipientCount)
            .Select(i => new BookingDayRevertRecipient($"Customer {i}", $"customer{i}@example.com"))
            .ToList();

        var integrationEvent = new BookingDayRevertedIntegrationEvent(
            BookingDayId,
            TenantId,
            "Thompson ↔ Lynn Lake",
            new DateOnly(2026, 9, 15),
            SeatsSold: 2,
            SeatsNeeded: 1,
            recipients,
            tripId,
            tripNumber);

        return eventId is { } id
            ? integrationEvent with { EventId = id }
            : integrationEvent;
    }

    [Fact]
    public async Task A_revert_sends_the_built_in_trip_at_risk_email_and_records_the_dispatch()
    {
        await Handler.Handle(Event(), CancellationToken.None);

        var batch = Assert.Single(_sender.Batches);
        Assert.Equal(2, batch.Count);
        Assert.Contains("1 seat(s) needed", batch[0].Subject);
        Assert.Contains("Thompson ↔ Lynn Lake", batch[0].Subject);
        Assert.Contains("Customer 1", batch[0].HtmlBody);
        Assert.Contains("<strong>1 more seat(s) are needed", batch[0].HtmlBody);
        Assert.Contains("Tuesday, September 15, 2026", batch[0].HtmlBody);

        var dispatch = Assert.Single(_dispatches.Dispatches);
        Assert.Equal(NotificationServiceType.CommunityBookingAtRisk, dispatch.ServiceType);
        Assert.Equal(Guid.Empty, dispatch.TemplateId);
        Assert.Equal(BookingDayRevertedIntegrationEventHandler.BuiltInTemplateName, dispatch.TemplateName);
        Assert.Equal(BookingDayId, dispatch.TripId);
        Assert.Equal("DAY-2026-09-15", dispatch.TripNumber);
        Assert.All(dispatch.Recipients, r => Assert.Equal(DispatchRecipientStatus.Sent, r.Status));
    }

    [Fact]
    public async Task More_than_sixteen_recipients_chunk_into_multiple_dispatches()
    {
        await Handler.Handle(Event(recipientCount: 20), CancellationToken.None);

        Assert.Equal(2, _sender.Batches.Count);
        Assert.Equal(EmailDispatch.MaxRecipients, _sender.Batches[0].Count);
        Assert.Equal(4, _sender.Batches[1].Count);

        Assert.Equal(2, _dispatches.Dispatches.Count);
        Assert.Equal(EmailDispatch.MaxRecipients, _dispatches.Dispatches[0].Recipients.Count);
        Assert.Equal(4, _dispatches.Dispatches[1].Recipients.Count);
        Assert.NotEqual(_dispatches.Dispatches[0].Id, _dispatches.Dispatches[1].Id);
    }

    [Fact]
    public async Task A_redelivered_event_sends_nothing_twice()
    {
        var integrationEvent = Event(recipientCount: 20);

        await Handler.Handle(integrationEvent, CancellationToken.None);
        await Handler.Handle(integrationEvent, CancellationToken.None);

        Assert.Equal(2, _sender.Batches.Count);
        Assert.Equal(2, _dispatches.Dispatches.Count);
    }

    [Fact]
    public void Dispatch_ids_are_deterministic_per_event_and_chunk()
    {
        var eventId = Guid.NewGuid();

        Assert.Equal(
            BookingDayRevertedIntegrationEventHandler.DeterministicDispatchId(eventId, 0),
            BookingDayRevertedIntegrationEventHandler.DeterministicDispatchId(eventId, 0));
        Assert.NotEqual(
            BookingDayRevertedIntegrationEventHandler.DeterministicDispatchId(eventId, 0),
            BookingDayRevertedIntegrationEventHandler.DeterministicDispatchId(eventId, 1));
        Assert.NotEqual(
            BookingDayRevertedIntegrationEventHandler.DeterministicDispatchId(eventId, 0),
            BookingDayRevertedIntegrationEventHandler.DeterministicDispatchId(Guid.NewGuid(), 0));
    }

    [Fact]
    public async Task An_active_community_booking_at_risk_template_overrides_the_built_in_body()
    {
        var template = EmailTemplate.Create(
            TenantId,
            "Save the shuttle",
            NotificationServiceType.CommunityBookingAtRisk,
            clientId: null,
            clientName: null,
            "Only {{SeatsNeeded}} seats to go!",
            "<p>{{PassengerName}}, {{SeatsNeeded}} seats save {{Route}}.</p>").Value;
        _templates.Templates.Add(template);

        await Handler.Handle(Event(), CancellationToken.None);

        var batch = Assert.Single(_sender.Batches);
        Assert.Equal("Only 1 seats to go!", batch[0].Subject);
        Assert.Contains("1 seats save Thompson ↔ Lynn Lake", batch[0].HtmlBody);

        var dispatch = Assert.Single(_dispatches.Dispatches);
        Assert.Equal(template.Id, dispatch.TemplateId);
        Assert.Equal("Save the shuttle", dispatch.TemplateName);
    }

    [Fact]
    public async Task The_trip_backlink_is_recorded_when_the_event_carries_it()
    {
        var tripId = Guid.NewGuid();

        await Handler.Handle(Event(tripId: tripId, tripNumber: "NL-1042"), CancellationToken.None);

        var dispatch = Assert.Single(_dispatches.Dispatches);
        Assert.Equal(tripId, dispatch.TripId);
        Assert.Equal("NL-1042", dispatch.TripNumber);
    }

    [Fact]
    public async Task Malformed_recipient_emails_are_skipped_not_failed()
    {
        var integrationEvent = Event() with
        {
            Recipients =
            [
                new BookingDayRevertRecipient("Good", "good@example.com"),
                new BookingDayRevertRecipient("Bad", "not-an-email"),
            ],
        };

        await Handler.Handle(integrationEvent, CancellationToken.None);

        var batch = Assert.Single(_sender.Batches);
        var email = Assert.Single(batch);
        Assert.Equal("good@example.com", email.To);
    }

    [Fact]
    public async Task No_reachable_recipients_means_nothing_is_sent_or_recorded()
    {
        await Handler.Handle(Event(recipientCount: 0), CancellationToken.None);

        Assert.Empty(_sender.Batches);
        Assert.Empty(_dispatches.Dispatches);
    }
}
