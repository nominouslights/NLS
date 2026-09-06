namespace NorthernLink.Trips.Domain.Trips;

/// <summary>
/// Derivations over <see cref="TripServiceType"/>. The member list itself is load-bearing
/// (byte-identical siblings in other modules join on the spellings) — new semantics belong
/// here, never as new members.
/// </summary>
public static class TripServiceTypeExtensions
{
    /// <summary>
    /// True for the freight services (Cargo and Grocery): runs that carry goods rather than
    /// passengers — no seats, no passenger-manifest start gate, demand not applicable.
    /// </summary>
    public static bool IsCargoService(this TripServiceType serviceType) =>
        serviceType is TripServiceType.Cargo or TripServiceType.Grocery;
}
