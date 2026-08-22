using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Shipments.SetSecured;

/// <summary>Per-item 'tied down' flag, set from the manifest's cargo section or the Driver Field App. Distinct from the manifest's trip-level all-cargo-secured attestation.</summary>
public sealed record SetShipmentSecuredCommand(Guid ShipmentId, bool Secured) : ICommand;
