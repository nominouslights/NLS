namespace NorthernLink.Trips.Domain.Stops;

/// <summary>
/// Optional classification of a stop, used later for map iconography. Stored as its
/// string name (nullable) — an unclassified stop is perfectly valid.
/// </summary>
public enum StopType
{
    Hub,
    Community,
    Airport,
    MineSite,
    PickupPoint,

    /// <summary>
    /// A venue Northern Link has a standing business relationship with and runs a corridor to or
    /// from — the Best Western in Thompson, the Lynn Inn in Lynn Lake, Leaf Rapids Town Hall. This
    /// is a commercial fact, not a kind of place: an airport we merely pick up from by request,
    /// and a by-request drop-off at a private address, are ordinary stops however often we call
    /// there. The distinction is what the terminus summary report (NL-TRM-01) is built on, so it
    /// is set deliberately by a dispatcher rather than inferred from route endpoints — a venue we
    /// deal with is a terminus before it has a route, and an airport standing in as an origin
    /// never becomes one.
    /// </summary>
    Terminus,
}
