using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;

namespace NorthernLink.Trips.Application.Shipments.GetShipments;

/// <summary>
/// The dispatcher's cargo queues, as one filtered list: unassigned pre-registrations, what is on
/// a given run, everything for one client across whatever trips carried it, freight stranded at a
/// hub, and the delivered-but-clientless backlog the manifest migration leaves behind.
/// </summary>
public sealed record GetShipmentsQuery(ShipmentListFilter Filter) : IQuery<ShipmentPageResponse>;
