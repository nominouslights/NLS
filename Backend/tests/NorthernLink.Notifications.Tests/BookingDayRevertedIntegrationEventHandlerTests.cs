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

    /// <summary>
    /// The event id behind the golden dispatch ids below. Fixed, not <c>Guid.NewGuid()</c> —
    /// the contract is the value, not the function's self-consistency.
    /// </summary>
    private static readonly Guid GoldenEventId = Guid.Parse("9a4d2f18-0c7b-4f3a-9e51-6b2c8d7a1e40");

    /// <summary>
    /// SHA-256("booking-day-reverted:9a4d2f180c7b4f3a9e516b2c8d7a1e40:chunk:{i}"), first 16
    /// bytes read as a Guid — computed outside this codebase, not captured from the method
    /// under test.
    /// </summary>
    private static readonly Guid[] GoldenDispatchIds =
    [
        Guid.Parse("0c191d60-c23b-8a71-0d86-f9a4471c38fe"),
        Guid.Parse("9e03f305-d2e9-6c12-29c5-8b9d7aa30b6e"),
        Guid.Parse("fa4d9871-b2ba-0c47-7b36-78764ead6c1d"),
    ];

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Dispatch_ids_match_their_golden_values(int chunkIndex)
    {
        // The real contract is the VALUE, not that a pure function equals itself. Every outbox
        // row still in flight was written against these ids: change the seed string, the hash,
        // or the byte-to-Guid fold and a redelivered event mints fresh ids, finds no recorded
        // chunk, and re-emails every recipient. That must fail here, loudly, not in someone's
        // inbox.
        Assert.Equal(
            GoldenDispatchIds[chunkIndex],
            BookingDayRevertedIntegrationEventHandler.DeterministicDispatchId(GoldenEventId, chunkIndex));
    }

    [Fact]
    public async Task A_sent_dispatch_lands_on_its_golden_id()
    {
        // Pins the wiring as well as the function: the handler must derive the id from the
        // event's own EventId and the chunk index, in that order.
        await Handler.Handle(Event(eventId: GoldenEventId), CancellationToken.None);

        var dispatch = Assert.Single(_dispatches.Dispatches);
        Assert.Equal(GoldenDispatchIds[0], dispatch.Id);
    }

    [Fact]
    public void Dispatch_ids_are_distinct_per_chunk_and_per_event()
    {
        Assert.Equal(GoldenDispatchIds.Length, GoldenDispatchIds.Distinct().Count());
        Assert.NotEqual(
            BookingDayRevertedIntegrationEventHandler.DeterministicDispatchId(GoldenEventId, 0),
            BookingDayRevertedIntegrationEventHandler.DeterministicDispatchId(Guid.NewGuid(), 0));
    }

    [Fact]
    public async Task A_crash_mid_event_resends_only_the_unrecorded_tail()
    {
        // The finer half of the redelivery invariant the handler documents: chunks are recorded
        // as they complete, so a process that died between chunk 1 and chunk 2 must, on
        // redelivery, skip the chunks already on record and send ONLY the gap. "All chunks
        // already recorded" (the test above) would still pass if the handler bailed out on the
        // first recorded chunk instead of continuing — this one would not.
        var integrationEvent = Event(recipientCount: 40, eventId: GoldenEventId);
        Assert.Equal(3, integrationEvent.Recipients.Count / EmailDispatch.MaxRecipients + 1);

        // Chunks 0 and 2 already went out before the crash; chunk 1 never did.
        _dispatches.Add(AlreadyRecorded(GoldenDispatchIds[0]));
        _dispatches.Add(AlreadyRecorded(GoldenDispatchIds[2]));

        await Handler.Handle(integrationEvent, CancellationToken.None);

        var batch = Assert.Single(_sender.Batches);
        Assert.Equal(EmailDispatch.MaxRecipients, batch.Count);
        // Chunk 1 is recipients 17..32 of the 40.
        Assert.Equal("customer17@example.com", batch[0].To);
        Assert.Equal("customer32@example.com", batch[^1].To);

        var recorded = Assert.Single(_dispatches.Dispatches, d => d.Id == GoldenDispatchIds[1]);
        Assert.Equal(EmailDispatch.MaxRecipients, recorded.Recipients.Count);
        Assert.Equal(3, _dispatches.Dispatches.Count);
    }

    /// <summary>A dispatch already on record under <paramref name="dispatchId"/> — the replay marker.</summary>
    private static EmailDispatch AlreadyRecorded(Guid dispatchId) =>
        EmailDispatch.Record(
            dispatchId,
            TenantId,
            tripId: BookingDayId,
            tripNumber: "DAY-2026-09-15",
            manifestId: null,
            templateId: Guid.Empty,
            templateName: BookingDayRevertedIntegrationEventHandler.BuiltInTemplateName,
            serviceType: NotificationServiceType.CommunityBookingAtRisk,
            clientId: null,
            clientName: null,
            recipients:
            [
                new DispatchRecipient
                {
                    Email = "already@example.com",
                    PassengerName = "Already Sent",
                    Status = DispatchRecipientStatus.Sent,
                },
            ]).Value;

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
