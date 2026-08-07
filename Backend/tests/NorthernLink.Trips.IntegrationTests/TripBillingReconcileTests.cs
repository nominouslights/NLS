using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Shared.IntegrationEvents.Billing;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Infrastructure.Persistence;
using Xunit;

namespace NorthernLink.Trips.IntegrationTests;

/// <summary>
/// Billing publishes an invoice's whole claim set, never a delta, and Trips reconciles against
/// it. These prove the properties the Trips screen depends on against a real Postgres: state
/// moves with the worksheet, dropped trips are released, and replays converge instead of
/// drifting.
/// </summary>
[Collection("postgres")]
public class TripBillingReconcileTests(PostgresFixture fixture)
{
    private static readonly Guid InvoiceId = Guid.Parse("00000000-0000-0000-0000-0000000b0001");
    private static readonly Guid OtherInvoiceId = Guid.Parse("00000000-0000-0000-0000-0000000b0002");
    private static readonly DateOnly Entered = new(2026, 8, 12);
    private static readonly DateOnly Received = new(2026, 8, 30);

    private static InvoiceBillingStateChangedIntegrationEvent Event(
        Guid invoiceId,
        string state,
        IReadOnlyList<Guid> tripIds,
        string? qbo = null,
        DateOnly? paid = null) =>
        new(
            invoiceId,
            PostgresFixture.TenantA,
            "INV-2026-114",
            state,
            tripIds,
            qbo,
            qbo is null ? null : Entered,
            paid);

    private async Task HandleAsync(InvoiceBillingStateChangedIntegrationEvent integrationEvent)
    {
        await using var context = fixture.CreateTripsContext(PostgresFixture.TenantA);
        var handler = new InvoiceBillingStateChangedIntegrationEventHandler(
            new TripBillingRepository(context),
            new TestTripRepository(context),
            NullLogger<InvoiceBillingStateChangedIntegrationEventHandler>.Instance);

        await handler.Handle(integrationEvent, CancellationToken.None);
    }

    /// <summary>Just enough of ITripRepository for the handler (the real one is internal).</summary>
    private sealed class TestTripRepository(TripsDbContext context) : ITripRepository
    {
        public void Add(Trip trip) => context.Trips.Add(trip);

        public Task<Trip?> GetByIdAsync(Guid tripId, CancellationToken cancellationToken = default) =>
            context.Trips.FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        public Task<Trip?> GetByTripNumberAsync(string tripNumber, CancellationToken cancellationToken = default) =>
            context.Trips.FirstOrDefaultAsync(t => t.TripNumber == tripNumber, cancellationToken);

        public async Task<IReadOnlyList<Trip>> GetByRoundTripKeyAsync(
            string roundTripKey, CancellationToken cancellationToken = default) =>
            await context.Trips.Where(t => t.RoundTripKey == roundTripKey).ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<Trip>> GetByIdsAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> tripIds,
            CancellationToken cancellationToken = default) =>
            await context.Trips
                .IgnoreQueryFilters()
                .Where(t => t.TenantId == tenantId && tripIds.Contains(t.Id))
                .ToListAsync(cancellationToken);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            context.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<TripBilling>> RowsForAsync(Guid invoiceId)
    {
        await using var context = fixture.CreateTripsContext(PostgresFixture.TenantA);
        return await context.TripBillings
            .AsNoTracking()
            .Where(b => b.InvoiceId == invoiceId)
            .OrderBy(b => b.TripId)
            .ToListAsync();
    }

    [Fact]
    public async Task A_draft_claim_lands_as_on_worksheet()
    {
        var trip = Guid.NewGuid();

        await HandleAsync(Event(InvoiceId, TripBillingStates.OnWorksheet, [trip]));

        var row = Assert.Single(await RowsForAsync(InvoiceId), b => b.TripId == trip);
        Assert.Equal(TripBillingStates.OnWorksheet, row.State);
        Assert.Equal("INV-2026-114", row.InvoiceNumber);
        Assert.Null(row.QboInvoiceId);
        Assert.Null(row.PaymentConfirmedDate);
    }

    [Fact]
    public async Task Entering_then_paying_moves_every_claimed_trip_forward()
    {
        var invoiceId = Guid.NewGuid();
        var trips = new[] { Guid.NewGuid(), Guid.NewGuid() };

        await HandleAsync(Event(invoiceId, TripBillingStates.OnWorksheet, trips));
        await HandleAsync(Event(invoiceId, TripBillingStates.Invoiced, trips, qbo: "QBO-8871"));

        var invoiced = await RowsForAsync(invoiceId);
        Assert.Equal(2, invoiced.Count);
        Assert.All(invoiced, b => Assert.Equal(TripBillingStates.Invoiced, b.State));
        Assert.All(invoiced, b => Assert.Equal("QBO-8871", b.QboInvoiceId));

        await HandleAsync(Event(invoiceId, TripBillingStates.Paid, trips, qbo: "QBO-8871", paid: Received));

        var paid = await RowsForAsync(invoiceId);
        Assert.All(paid, b => Assert.Equal(TripBillingStates.Paid, b.State));
        Assert.All(paid, b => Assert.Equal(Received, b.PaymentConfirmedDate));
    }

    [Fact]
    public async Task A_trip_dropped_from_the_claim_set_is_released()
    {
        var invoiceId = Guid.NewGuid();
        var kept = Guid.NewGuid();
        var dropped = Guid.NewGuid();

        await HandleAsync(Event(invoiceId, TripBillingStates.OnWorksheet, [kept, dropped]));
        // The line pricing `dropped` was removed — the next event simply omits it.
        await HandleAsync(Event(invoiceId, TripBillingStates.OnWorksheet, [kept]));

        var rows = await RowsForAsync(invoiceId);
        Assert.Equal(kept, Assert.Single(rows).TripId);
    }

    [Fact]
    public async Task Voiding_releases_every_claim()
    {
        var invoiceId = Guid.NewGuid();
        var trips = new[] { Guid.NewGuid(), Guid.NewGuid() };

        await HandleAsync(Event(invoiceId, TripBillingStates.OnWorksheet, trips));
        await HandleAsync(Event(invoiceId, TripBillingStates.Released, []));

        // No row at all, rather than a stored "Released" state — "is this billable" stays
        // a single existence check.
        Assert.Empty(await RowsForAsync(invoiceId));
    }

    [Fact]
    public async Task Replaying_the_same_event_is_idempotent()
    {
        var invoiceId = Guid.NewGuid();
        var trips = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var settled = Event(invoiceId, TripBillingStates.Paid, trips, qbo: "QBO-8871", paid: Received);

        await HandleAsync(settled);
        await HandleAsync(settled);
        await HandleAsync(settled);

        var rows = await RowsForAsync(invoiceId);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, b => Assert.Equal(TripBillingStates.Paid, b.State));
    }

    [Fact]
    public async Task A_trip_moved_to_another_invoice_ends_up_claimed_by_only_that_one()
    {
        var trip = Guid.NewGuid();

        await HandleAsync(Event(InvoiceId, TripBillingStates.OnWorksheet, [trip]));
        // The first draft was voided and the trip pulled onto a second worksheet.
        await HandleAsync(Event(InvoiceId, TripBillingStates.Released, []));
        await HandleAsync(Event(OtherInvoiceId, TripBillingStates.OnWorksheet, [trip]));

        Assert.Empty(await RowsForAsync(InvoiceId));
        Assert.Equal(trip, Assert.Single(await RowsForAsync(OtherInvoiceId)).TripId);
    }

    [Fact]
    public async Task A_stale_claim_under_another_invoice_does_not_break_the_primary_key()
    {
        var trip = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await HandleAsync(Event(first, TripBillingStates.OnWorksheet, [trip]));
        // A missed "released" event would otherwise leave the row under `first` and collide
        // on the trip_id primary key — the reconcile clears the stale row instead.
        await HandleAsync(Event(second, TripBillingStates.Invoiced, [trip], qbo: "QBO-9002"));

        Assert.Empty(await RowsForAsync(first));
        var row = Assert.Single(await RowsForAsync(second));
        Assert.Equal(TripBillingStates.Invoiced, row.State);
        Assert.Equal("QBO-9002", row.QboInvoiceId);
    }

    // --- The billing-driven trip status: the handler now advances the Trip aggregate too ----

    private async Task<Guid> SeedReadyForBillingTripAsync(string tripNumber)
    {
        await using var context = fixture.CreateTripsContext(PostgresFixture.TenantA);

        var existing = await context.Trips.SingleOrDefaultAsync(t => t.TripNumber == tripNumber);
        if (existing is not null)
        {
            return existing.Id;
        }

        var trip = Trip.Schedule(
            PostgresFixture.TenantA,
            tripNumber,
            serviceDate: DateOnly.FromDateTime(DateTime.UtcNow.Date),
            windowStart: new TimeOnly(8, 0),
            windowEnd: null,
            serviceType: TripServiceType.ContractCrew,
            routeId: null,
            routeName: "Thompson – Lynn Lake",
            origin: "Thompson",
            destination: "Lynn Lake",
            stops: Array.Empty<RouteStop>(),
            distanceKm: 320,
            scheduleTemplateId: null,
            roundTripKey: null,
            direction: null,
            isEmptyLeg: false,
            clientId: Guid.NewGuid(),
            clientName: "Alamos Gold",
            poNumber: null,
            driverId: null,
            driverName: null,
            vehicleId: null,
            vehicleUnit: null,
            seatsCapacity: null,
            seatsMinimum: null);
        Assert.True(trip.IsSuccess, trip.IsFailure ? trip.Error.Code : "");
        Assert.True(trip.Value.RecordPostTripInspection().IsSuccess);
        Assert.True(trip.Value.FinishOperations().IsSuccess);
        Assert.Equal(TripStatus.ReadyForBilling, trip.Value.Status);

        context.Trips.Add(trip.Value);
        await context.SaveChangesAsync();
        return trip.Value.Id;
    }

    private async Task<TripStatus> StatusOfAsync(Guid tripId)
    {
        await using var context = fixture.CreateTripsContext(PostgresFixture.TenantA);
        return (await context.Trips.AsNoTracking().SingleAsync(t => t.Id == tripId)).Status;
    }

    [Fact]
    public async Task A_draft_claim_leaves_the_trip_ready_for_billing()
    {
        var tripId = await SeedReadyForBillingTripAsync("TR-BD-0001");

        await HandleAsync(Event(Guid.NewGuid(), TripBillingStates.OnWorksheet, [tripId]));

        // A draft is not an invoice — only entry into QuickBooks moves the trip.
        Assert.Equal(TripStatus.ReadyForBilling, await StatusOfAsync(tripId));
    }

    [Fact]
    public async Task Entering_paying_and_clearing_walk_the_trip_through_the_billing_arc()
    {
        var tripId = await SeedReadyForBillingTripAsync("TR-BD-0002");
        var invoiceId = Guid.NewGuid();

        await HandleAsync(Event(invoiceId, TripBillingStates.Invoiced, [tripId], qbo: "QBO-9101"));
        Assert.Equal(TripStatus.Invoiced, await StatusOfAsync(tripId));

        await HandleAsync(Event(invoiceId, TripBillingStates.Paid, [tripId], qbo: "QBO-9101", paid: Received));
        Assert.Equal(TripStatus.Completed, await StatusOfAsync(tripId));

        // Payment confirmation cleared in error — the trip honestly steps back.
        await HandleAsync(Event(invoiceId, TripBillingStates.Invoiced, [tripId], qbo: "QBO-9101"));
        Assert.Equal(TripStatus.Invoiced, await StatusOfAsync(tripId));
    }

    [Fact]
    public async Task A_write_off_lands_the_trip_in_written_off_and_keeps_the_claim()
    {
        var tripId = await SeedReadyForBillingTripAsync("TR-BD-0003");
        var invoiceId = Guid.NewGuid();

        await HandleAsync(Event(invoiceId, TripBillingStates.Invoiced, [tripId], qbo: "QBO-9102"));
        await HandleAsync(new InvoiceBillingStateChangedIntegrationEvent(
            invoiceId, PostgresFixture.TenantA, "INV-2026-114", TripBillingStates.WrittenOff,
            [tripId], "QBO-9102", Entered, null, "Client insolvent"));

        Assert.Equal(TripStatus.WrittenOff, await StatusOfAsync(tripId));

        await using var context = fixture.CreateTripsContext(PostgresFixture.TenantA);
        var trip = await context.Trips.AsNoTracking().SingleAsync(t => t.Id == tripId);
        Assert.Equal("Client insolvent", trip.WrittenOffReason);
        // The claim row survives a write-off — the trip must never look billable again.
        var row = Assert.Single(await RowsForAsync(invoiceId));
        Assert.Equal(TripBillingStates.WrittenOff, row.State);
    }

    [Fact]
    public async Task A_release_never_demotes_an_invoiced_trip()
    {
        var tripId = await SeedReadyForBillingTripAsync("TR-BD-0004");
        var invoiceId = Guid.NewGuid();

        await HandleAsync(Event(invoiceId, TripBillingStates.Invoiced, [tripId], qbo: "QBO-9103"));
        // Released is only reachable from a voided DRAFT; if one ever arrives against an
        // invoiced trip it must not walk the status back — Invoiced → ReadyForBilling is the
        // one move the lifecycle forbids outright.
        await HandleAsync(Event(invoiceId, TripBillingStates.Released, []));

        Assert.Equal(TripStatus.Invoiced, await StatusOfAsync(tripId));
    }

    [Fact]
    public async Task A_replayed_older_event_does_not_regress_a_paid_trip()
    {
        var tripId = await SeedReadyForBillingTripAsync("TR-BD-0005");
        var invoiceId = Guid.NewGuid();

        // Capture the Invoiced event FIRST so its OccurredAtUtc predates the Paid event —
        // then replay it after payment. Same-status guards can't catch this (Completed →
        // Invoiced is legal); only the per-trip high-water mark does.
        var invoicedEvent = Event(invoiceId, TripBillingStates.Invoiced, [tripId], qbo: "QBO-9104");
        await HandleAsync(invoicedEvent);
        await HandleAsync(Event(invoiceId, TripBillingStates.Paid, [tripId], qbo: "QBO-9104", paid: Received));
        Assert.Equal(TripStatus.Completed, await StatusOfAsync(tripId));

        await HandleAsync(invoicedEvent); // stale replay

        Assert.Equal(TripStatus.Completed, await StatusOfAsync(tripId));
        var row = Assert.Single(await RowsForAsync(invoiceId));
        Assert.Equal(TripBillingStates.Paid, row.State); // replica held the line too
    }

    [Fact]
    public async Task A_cancelled_trip_in_a_claim_set_is_skipped_not_thrown()
    {
        await using (var context = fixture.CreateTripsContext(PostgresFixture.TenantA))
        {
            var existing = await context.Trips.SingleOrDefaultAsync(t => t.TripNumber == "TR-BD-0006");
            if (existing is null)
            {
                var trip = Trip.Schedule(
                    PostgresFixture.TenantA, "TR-BD-0006",
                    DateOnly.FromDateTime(DateTime.UtcNow.Date), new TimeOnly(8, 0), null,
                    TripServiceType.ContractCrew, null, "Thompson – Lynn Lake", "Thompson", "Lynn Lake",
                    Array.Empty<RouteStop>(), 320, null, null, null, false,
                    Guid.NewGuid(), "Alamos Gold", null, null, null, null, null, null, null).Value;
                Assert.True(trip.Cancel("Weather").IsSuccess);
                context.Trips.Add(trip);
                await context.SaveChangesAsync();
            }
        }

        Guid cancelledId;
        await using (var context = fixture.CreateTripsContext(PostgresFixture.TenantA))
        {
            cancelledId = (await context.Trips.SingleAsync(t => t.TripNumber == "TR-BD-0006")).Id;
        }

        // Cancelled → Invoiced is illegal; the handler logs and continues rather than failing
        // the outbox row (a throw would block the whole billing schema for that poll).
        await HandleAsync(Event(Guid.NewGuid(), TripBillingStates.Invoiced, [cancelledId], qbo: "QBO-9105"));

        Assert.Equal(TripStatus.Cancelled, await StatusOfAsync(cancelledId));
    }
}
