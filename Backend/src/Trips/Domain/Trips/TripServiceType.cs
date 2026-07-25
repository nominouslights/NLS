namespace NorthernLink.Trips.Domain.Trips;

/// <summary>
/// What kind of service a trip (or schedule template) runs. Declared per-module, never
/// shared: integration events carry the value as a string, and other modules (Clients,
/// Billing) declare their own equivalent. "ContractCrew" is the platform name for the
/// Alamos crew-shuttle service — the client brand never leaks into the domain model.
/// </summary>
public enum TripServiceType
{
    ContractCrew,
    Community,
    Nihb,
    Charter,
    Cargo,
    Grocery,
}
