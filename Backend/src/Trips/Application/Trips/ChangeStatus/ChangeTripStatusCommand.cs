using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.ChangeStatus;

/// <summary>
/// Moves a trip through its lifecycle: InProgress (start), Completed (raises the
/// trip-completed integration event for Billing), or Cancelled (with an optional
/// reason). Scheduled is a creation state, never a transition target.
/// </summary>
public sealed record ChangeTripStatusCommand(
    Guid TripId,
    TripStatus Status,
    string? Reason) : ICommand;
