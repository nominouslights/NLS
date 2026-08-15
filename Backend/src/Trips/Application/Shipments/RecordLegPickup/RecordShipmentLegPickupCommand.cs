using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Shipments.RecordLegPickup;

/// <summary>Records the freight going onto one leg's trip. Legs run in order.</summary>
public sealed record RecordShipmentLegPickupCommand(Guid ShipmentId, int Sequence, DateTimeOffset? AtUtc, string? By) : ICommand;
