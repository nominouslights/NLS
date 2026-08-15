using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Shipments;

/// <summary>All domain errors the Shipment aggregate (and its handlers) can produce.</summary>
public static class ShipmentErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Trips.Shipment.NotFound", "The shipment was not found.");

    public static readonly Error ShipmentNumberRequired = Error.Validation(
        "Trips.Shipment.ShipmentNumberRequired", "A shipment number is required.");

    public static readonly Error DescriptionRequired = Error.Validation(
        "Trips.Shipment.DescriptionRequired", "A description of the goods is required.");

    public static readonly Error InvalidPieces = Error.Validation(
        "Trips.Shipment.InvalidPieces", "A shipment must have at least one piece.");

    public static readonly Error InvalidWeight = Error.Validation(
        "Trips.Shipment.InvalidWeight", "Weight cannot be negative.");

    public static readonly Error InvalidDimensions = Error.Validation(
        "Trips.Shipment.InvalidDimensions", "Dimensions cannot be negative.");

    public static readonly Error InvalidCharge = Error.Validation(
        "Trips.Shipment.InvalidCharge", "The charge must be a non-negative amount with at most two decimal places.");

    public static readonly Error InvalidDeclaredValue = Error.Validation(
        "Trips.Shipment.InvalidDeclaredValue", "The declared value must be a non-negative amount with at most two decimal places.");

    public static readonly Error EnteredByRequired = Error.Validation(
        "Trips.Shipment.EnteredByRequired", "A dispatcher-entered shipment must record who entered it.");

    public static readonly Error RequiredByBeforeReady = Error.Validation(
        "Trips.Shipment.RequiredByBeforeReady", "The required-by date cannot fall before the ready date.");

    public static readonly Error PaymentMethodRequired = Error.Validation(
        "Trips.Shipment.PaymentMethodRequired", "A collected amount needs a payment method.");

    public static readonly Error ChargeRequired = Error.Validation(
        "Trips.Shipment.ChargeRequired", "A cash or online payment needs an amount.");

    public static readonly Error WaivedChargeMustBeZero = Error.Validation(
        "Trips.Shipment.WaivedChargeMustBeZero", "A waived shipment cannot carry a charge.");

    public static readonly Error PaymentMethodOnClientShipment = Error.Validation(
        "Trips.Shipment.PaymentMethodOnClientShipment",
        "A counter payment method belongs to a clientless shipment — a shipment billed to a client is settled by invoice.");

    public static readonly Error ClientNameRequired = Error.Validation(
        "Trips.Shipment.ClientNameRequired", "Attaching a client requires the client's name snapshot.");

    public static readonly Error ClientNotFound = Error.NotFound(
        "Trips.Shipment.ClientNotFound", "The client to bill was not found.");

    public static readonly Error TripNotFound = Error.NotFound(
        "Trips.Shipment.TripNotFound", "The trip to route this shipment through was not found.");

    public static readonly Error TripNumberRequired = Error.Validation(
        "Trips.Shipment.TripNumberRequired", "Routing a shipment through a trip requires the trip number snapshot.");

    public static readonly Error TripOperationallyClosed = Error.Conflict(
        "Trips.Shipment.TripOperationallyClosed", "The run has already happened — cargo can no longer be added to it.");

    public static readonly Error LegNotFound = Error.NotFound(
        "Trips.Shipment.LegNotFound", "The shipment does not have a leg with that sequence.");

    public static readonly Error LegAlreadyOnTrip = Error.Conflict(
        "Trips.Shipment.LegAlreadyOnTrip", "The shipment is already routed through that trip.");

    public static readonly Error LegNotRemovable = Error.Conflict(
        "Trips.Shipment.LegNotRemovable", "Only a planned leg can be removed — freight that has already been picked up stays on the record.");

    public static readonly Error LegNotPlanned = Error.Conflict(
        "Trips.Shipment.LegNotPlanned", "Only a planned leg can be picked up.");

    public static readonly Error LegNotPickedUp = Error.Conflict(
        "Trips.Shipment.LegNotPickedUp", "Only a picked-up leg can be dropped.");

    public static readonly Error EarlierLegOutstanding = Error.Conflict(
        "Trips.Shipment.EarlierLegOutstanding", "An earlier leg of this shipment has not finished yet — legs run in order.");

    public static readonly Error NotEditable = Error.Conflict(
        "Trips.Shipment.NotEditable", "A cancelled or written-off shipment can no longer be edited.");

    public static readonly Error BillingNotEditable = Error.Conflict(
        "Trips.Shipment.BillingNotEditable",
        "An invoice already speaks for this shipment — reprice it on the invoice, not here.");

    public static readonly Error WriteOffReasonRequired = Error.Validation(
        "Trips.Shipment.WriteOffReasonRequired", "Closing a shipment without billing requires a reason.");

    public static readonly Error BillingStateOnClientlessShipment = Error.Conflict(
        "Trips.Shipment.BillingStateOnClientlessShipment",
        "A shipment with no client never enters the billing arc, so no invoice can speak for it.");

    public static readonly Error InvalidStatusFilter = Error.Validation(
        "Trips.Shipment.InvalidStatusFilter", "The status filter is not a known shipment status.");

    public static readonly Error InvalidKindFilter = Error.Validation(
        "Trips.Shipment.InvalidKindFilter", "The kind filter is not a known shipment kind.");

    public static Error InvalidStatusTransition(ShipmentStatus from, ShipmentStatus to) => Error.Conflict(
        "Trips.Shipment.InvalidStatusTransition", $"A shipment cannot move from {from} to {to}.");

    public static Error OperationallyClosed(ShipmentStatus status) => Error.Conflict(
        "Trips.Shipment.OperationallyClosed",
        $"The freight has already been handed over — a {status} shipment's routing can no longer be changed.");

    public static Error SystemDrivenStatus(ShipmentStatus status) => Error.Conflict(
        "Trips.Shipment.SystemDrivenStatus",
        $"{status} is set by Billing when the worksheet is entered in QuickBooks, paid, or written off — it cannot be set by hand.");
}
