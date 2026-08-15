namespace NorthernLink.Trips.Domain.Shipments;

/// <summary>
/// What sort of freight this is. Carried for filtering and labelling only — <b>no behaviour
/// hangs off it</b>, deliberately. It exists now because the alternative is a migration plus a
/// backfill guess later: the weekly grocery run has real distinguishing traits (batch cutoffs,
/// tiers, chilled vs dry) and if that distinction is ever needed, the column has to have been
/// recorded from the start.
/// <para>
/// Declared per-module, never shared — the integration event carries it as a string, exactly
/// like <see cref="Trips.TripServiceType"/>.
/// </para>
/// </summary>
public enum ShipmentKind
{
    Parcel,
    Grocery,
    Freight,
}
