using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Shipments.RecordDelivery;

/// <summary>The goods reached the consignee. The billing trigger: a shipment with a client and a charge lands in ReadyForBilling and feeds Billing; anything else lands in Delivered and never enters the billing arc.</summary>
public sealed record RecordShipmentDeliveryCommand(Guid ShipmentId, DateTimeOffset? AtUtc, string? ReceivedBy, string? Note) : ICommand;
