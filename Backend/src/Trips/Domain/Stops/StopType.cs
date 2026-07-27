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
}
