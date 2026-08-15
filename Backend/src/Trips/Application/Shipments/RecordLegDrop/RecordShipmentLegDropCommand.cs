using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Shipments.RecordLegDrop;

/// <summary>Records the freight coming off one leg's trip — at a hub, or at the destination. Not the handover: that is RecordShipmentDelivery, and only it earns the charge.</summary>
public sealed record RecordShipmentLegDropCommand(Guid ShipmentId, int Sequence, DateTimeOffset? AtUtc, string? By) : ICommand;
