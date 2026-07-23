namespace NorthernLink.Trips.Domain.Trips;

/// <summary>
/// Lifecycle of a trip. Deliberately minimal: "open — needs coverage" (Scheduled with no
/// driver) and "empty leg" (<c>Trip.IsEmptyLeg</c>) are frontend derivations, never
/// persisted statuses.
/// </summary>
public enum TripStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled,
}
