using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.Reopen;

/// <summary>
/// Undoes a dispatcher's lifecycle mistake — a trip cancelled, started, finished, or closed
/// without billing in error — by reopening it to Scheduled with a required reason
/// (<c>Trip.Reopen</c> holds the source → target map). A paid trip is never reopened, and neither
/// is any trip Billing still claims: the reason is journaled on the reopen event and shows on the
/// trip's activity timeline.
/// <para>
/// <see cref="ExpectedStatus"/> is the optional optimistic check: the status the dispatcher saw
/// when they chose to reopen. When present and no longer the trip's status, the reopen is refused
/// with <c>Trips.Trip.ReopenStatusChanged</c>; when absent, no check is made.
/// </para>
/// </summary>
public sealed record ReopenTripCommand(Guid TripId, string Reason, TripStatus? ExpectedStatus = null) : ICommand;
