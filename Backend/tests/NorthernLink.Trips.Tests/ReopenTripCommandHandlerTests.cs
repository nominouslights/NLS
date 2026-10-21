using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Application.Trips.Reopen;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// The billing-claim guards on reopen: any claim on the trip refuses (with the error matching the
/// claim's state — Paid gives the paid error), and a cancelled/closed-out leg whose round-trip
/// partner is claimed is refused so the run is never billed twice.
/// </summary>
public class ReopenTripCommandHandlerTests
{
    private readonly FakeTripRepository _trips = new();
    private readonly FakeTripBillingRepository _billing = new();

    private ReopenTripCommandHandler Handler => new(_trips, _billing);

    private Trip ReadyForBillingTrip(string tripNumber = "TR-1001", string? roundTripKey = null, TripDirection? direction = null)
    {
        var trip = TestPlanning.ScheduleTrip(
            tripNumber: tripNumber, clientId: Guid.NewGuid(), roundTripKey: roundTripKey, direction: direction).Value;
        trip.RecordPostTripInspection();
        Assert.True(trip.FinishOperations().IsSuccess);
        _trips.Add(trip);
        return trip;
    }

    private void Claim(Trip trip, string state) => _billing.Rows.Add(new TripBilling
    {
        TripId = trip.Id,
        TenantId = trip.TenantId,
        InvoiceId = Guid.NewGuid(),
        InvoiceNumber = "INV-2026-114",
        State = state,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    });

    [Fact]
    public async Task An_unclaimed_trip_reopens_and_saves()
    {
        var trip = ReadyForBillingTrip();

        var result = await Handler.Handle(new ReopenTripCommand(trip.Id, "Finished by mistake"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task A_draft_worksheet_claim_is_refused_with_void_the_draft_first()
    {
        var trip = ReadyForBillingTrip();
        Claim(trip, "OnWorksheet");

        var result = await Handler.Handle(new ReopenTripCommand(trip.Id, "Finished by mistake"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.OnWorksheetCannotReopen, result.Error);
        Assert.Equal(TripStatus.ReadyForBilling, trip.Status);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task A_paid_claim_gives_the_paid_error()
    {
        var trip = ReadyForBillingTrip();
        Assert.True(trip.MarkInvoiced().IsSuccess);
        Assert.True(trip.MarkPaid().IsSuccess);
        Claim(trip, "Paid");

        var result = await Handler.Handle(new ReopenTripCommand(trip.Id, "Mistake"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.PaidCannotReopen, result.Error);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task An_invoiced_claim_gives_the_invoiced_error()
    {
        var trip = ReadyForBillingTrip();
        Assert.True(trip.MarkInvoiced().IsSuccess);
        Claim(trip, "Invoiced");

        var result = await Handler.Handle(new ReopenTripCommand(trip.Id, "Mistake"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.InvoicedCannotReopen, result.Error);
    }

    [Fact]
    public async Task An_invoice_write_off_is_told_apart_from_a_close_out_by_its_claim_and_refused()
    {
        // Same WrittenOff status as a dispatcher close-out — the never-released claim is the tell.
        var trip = ReadyForBillingTrip();
        Assert.True(trip.MarkInvoiced().IsSuccess);
        Assert.True(trip.WriteOff("Client went bankrupt").IsSuccess);
        Claim(trip, "WrittenOff");

        var result = await Handler.Handle(new ReopenTripCommand(trip.Id, "Mistake"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.InvoiceWrittenOffCannotReopen, result.Error);
        Assert.Equal(TripStatus.WrittenOff, trip.Status);
    }

    [Fact]
    public async Task A_dispatcher_close_out_with_no_claim_reopens()
    {
        var trip = ReadyForBillingTrip();
        Assert.True(trip.CloseWithoutBilling("No contract").IsSuccess);

        var result = await Handler.Handle(new ReopenTripCommand(trip.Id, "Contract found"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
    }

    [Fact]
    public async Task A_cancelled_leg_whose_partner_is_claimed_is_refused()
    {
        var outbound = ReadyForBillingTrip("TR-2001", "merge:abc", TripDirection.Outbound);
        Claim(outbound, "OnWorksheet");

        var inbound = TestPlanning.ScheduleTrip(
            tripNumber: "TR-2002", clientId: Guid.NewGuid(), roundTripKey: "merge:abc", direction: TripDirection.Inbound).Value;
        Assert.True(inbound.Cancel("Crew change moved").IsSuccess);
        _trips.Add(inbound);

        var result = await Handler.Handle(new ReopenTripCommand(inbound.Id, "Cancelled by mistake"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.PartnerLegClaimedCannotReopen("TR-2001"), result.Error);
        Assert.Contains("TR-2001", result.Error.Message);
        Assert.Equal(TripStatus.Cancelled, inbound.Status);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task A_cancelled_leg_with_an_unclaimed_partner_reopens()
    {
        ReadyForBillingTrip("TR-2001", "merge:abc", TripDirection.Outbound);
        var inbound = TestPlanning.ScheduleTrip(
            tripNumber: "TR-2002", clientId: Guid.NewGuid(), roundTripKey: "merge:abc", direction: TripDirection.Inbound).Value;
        Assert.True(inbound.Cancel(null).IsSuccess);
        _trips.Add(inbound);

        var result = await Handler.Handle(new ReopenTripCommand(inbound.Id, "Cancelled by mistake"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.Scheduled, inbound.Status);
    }

    [Theory]
    [InlineData(TripStatus.ReadyForBilling)]
    [InlineData(TripStatus.WrittenOff)]
    public async Task The_partner_claim_guard_runs_for_every_status_that_touches_the_billable_pool(TripStatus status)
    {
        // A reopened leg leaves (or never re-enters) the billable pool and comes back later as a
        // separate group — behind a partner that is already drafted alone, the run bills twice.
        var outbound = ReadyForBillingTrip("TR-2001", "merge:abc", TripDirection.Outbound);
        Claim(outbound, "OnWorksheet");

        var inbound = TestPlanning.ScheduleTrip(
            tripNumber: "TR-2002", clientId: Guid.NewGuid(), roundTripKey: "merge:abc", direction: TripDirection.Inbound).Value;
        Assert.True(inbound.Start().IsSuccess);
        inbound.RecordPostTripInspection();
        Assert.True(inbound.FinishOperations().IsSuccess);

        if (status == TripStatus.WrittenOff)
        {
            Assert.True(inbound.CloseWithoutBilling("No contract").IsSuccess);
        }

        Assert.Equal(status, inbound.Status);
        _trips.Add(inbound);

        var result = await Handler.Handle(new ReopenTripCommand(inbound.Id, "Mistake"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.PartnerLegClaimedCannotReopen("TR-2001"), result.Error);
        Assert.Equal(status, inbound.Status);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task An_accidental_start_can_be_undone_even_when_the_partner_is_claimed()
    {
        // InProgress was never in the billable pool: leaving it InProgress and finishing bills
        // the same as reopening then finishing, so the partner guard must not block the undo.
        var outbound = ReadyForBillingTrip("TR-2001", "merge:abc", TripDirection.Outbound);
        Claim(outbound, "OnWorksheet");

        var inbound = TestPlanning.ScheduleTrip(
            tripNumber: "TR-2002", clientId: Guid.NewGuid(), roundTripKey: "merge:abc", direction: TripDirection.Inbound).Value;
        Assert.True(inbound.Start().IsSuccess);
        _trips.Add(inbound);

        var result = await Handler.Handle(new ReopenTripCommand(inbound.Id, "Started by mistake"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.Scheduled, inbound.Status);
    }

    [Fact]
    public async Task A_matching_expected_status_reopens()
    {
        var trip = ReadyForBillingTrip();

        var result = await Handler.Handle(
            new ReopenTripCommand(trip.Id, "Finished by mistake", TripStatus.ReadyForBilling), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task A_stale_expected_status_is_refused_so_a_double_click_reopens_once()
    {
        var trip = ReadyForBillingTrip();

        var first = await Handler.Handle(
            new ReopenTripCommand(trip.Id, "Finished by mistake", TripStatus.ReadyForBilling), CancellationToken.None);
        var second = await Handler.Handle(
            new ReopenTripCommand(trip.Id, "Finished by mistake", TripStatus.ReadyForBilling), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
        Assert.Equal(TripErrors.ReopenStatusChanged, second.Error);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task The_status_check_runs_before_the_claim_guard()
    {
        var trip = ReadyForBillingTrip();
        Claim(trip, "OnWorksheet");

        var result = await Handler.Handle(
            new ReopenTripCommand(trip.Id, "Mistake", TripStatus.Cancelled), CancellationToken.None);

        Assert.Equal(TripErrors.ReopenStatusChanged, result.Error);
    }

    [Fact]
    public async Task Reason_is_required()
    {
        var trip = ReadyForBillingTrip();

        var result = await Handler.Handle(new ReopenTripCommand(trip.Id, " "), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.ReopenReasonRequired, result.Error);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Unknown_trip_is_not_found()
    {
        var result = await Handler.Handle(new ReopenTripCommand(Guid.NewGuid(), "reason"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.NotFound, result.Error);
    }
}
