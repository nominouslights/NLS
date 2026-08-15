using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Shipments.GetById;

public sealed record GetShipmentByIdQuery(Guid ShipmentId) : IQuery<ShipmentResponse>;
