using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Shipments.CloseWithoutBilling;

/// <summary>The dispatcher's escape for freight that will never be invoiced. Without it ReadyForBilling would be a state with no exit, since every other way out is driven by an invoice that will never exist.</summary>
public sealed record CloseShipmentWithoutBillingCommand(Guid ShipmentId, string Reason) : ICommand;
