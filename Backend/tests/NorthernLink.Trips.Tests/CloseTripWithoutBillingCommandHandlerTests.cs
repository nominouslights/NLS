using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Application.Trips.CloseWithoutBilling;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class CloseTripWithoutBillingCommandHandlerTests
{
    private readonly FakeTripRepository _trips = new();
    private readonly FakeTripBillingRepository _billing = new();

    private CloseTripWithoutBillingCommandHandler Handler => new(_trips, _billing);

    private Trip ReadyForBillingTrip()
    {
        var trip = TestPlanning.ScheduleTrip(clientId: Guid.NewGuid()).Value;
        trip.RecordPostTripInspection();
        Assert.True(trip.FinishOperations().IsSuccess);
        _trips.Add(trip);
        return trip;
    }

    [Fact]
    public async Task Closes_an_unclaimed_ready_for_billing_trip_with_the_reason()
    {
        var trip = ReadyForBillingTrip();

        var result = await Handler.Handle(
            new CloseTripWithoutBillingCommand(trip.Id, "Client has no active contract"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.WrittenOff, trip.Status);
        Assert.Equal("Client has no active contract", trip.WrittenOffReason);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task Refused_while_a_worksheet_claims_the_trip()
    {
        var trip = ReadyForBillingTrip();
        _billing.Rows.Add(new TripBilling
        {
            TripId = trip.Id,
            TenantId = trip.TenantId,
            InvoiceId = Guid.NewGuid(),
            InvoiceNumber = "INV-2026-114",
            State = "OnWorksheet",
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });

        var result = await Handler.Handle(
            new CloseTripWithoutBillingCommand(trip.Id, "no contract"), CancellationToken.None);

        // Closing it out from under a draft would leave the two modules telling different
        // stories — the right move is on the Billing side (void the draft, or write it off).
        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.OnWorksheetCannotCloseWithoutBilling, result.Error);
        Assert.Equal(TripStatus.ReadyForBilling, trip.Status);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Reason_is_required()
    {
        var trip = ReadyForBillingTrip();

        var result = await Handler.Handle(
            new CloseTripWithoutBillingCommand(trip.Id, "  "), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.WriteOffReasonRequired, result.Error);
    }

    [Fact]
    public async Task Unknown_trip_is_not_found()
    {
        var result = await Handler.Handle(
            new CloseTripWithoutBillingCommand(Guid.NewGuid(), "reason"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.NotFound, result.Error);
    }
}
