using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.ChangeStatus;

/// <summary>
/// Moves a trip through the two transitions a dispatcher can name outright: InProgress (start)
/// and Cancelled (with an optional reason).
/// <para>
/// Everything after the run has its own command — <c>FinishTripOperationsCommand</c> because the
/// landing status depends on whether the trip has a client, and
/// <c>CloseTripWithoutBillingCommand</c> because it needs a reason. Invoiced and the
/// invoice-driven WrittenOff are set by Billing's events and are refused here outright.
/// </para>
/// </summary>
public sealed record ChangeTripStatusCommand(
    Guid TripId,
    TripStatus Status,
    string? Reason) : ICommand;
