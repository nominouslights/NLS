using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Trips.FinishOperations;

/// <summary>
/// Records that a trip's run is over. Deliberately not a status target on
/// <c>ChangeTripStatusCommand</c>: the resulting status depends on the trip, not the caller —
/// a client trip lands in ReadyForBilling and feeds Billing a billable trip, a clientless run
/// (community, walk-up charter) lands straight in Completed. "Set status to X" would be a
/// contract the caller cannot honour.
/// </summary>
public sealed record FinishTripOperationsCommand(Guid TripId) : ICommand;
