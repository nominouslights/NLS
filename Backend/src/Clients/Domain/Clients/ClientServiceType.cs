namespace NorthernLink.Clients.Domain.Clients;

/// <summary>
/// The primary service a client organization buys. Declared per-module (never shared):
/// integration events carry the name as a string, so other modules never reference this enum.
/// </summary>
public enum ClientServiceType
{
    ContractCrew,
    Community,
    Nihb,
    Charter,
    Cargo,
    Grocery,
}
