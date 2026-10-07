using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips;

/// <summary>All domain errors the Trip aggregate (and its handlers) can produce.</summary>
public static class TripErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Trips.Trip.NotFound", "The trip was not found.");

    public static readonly Error TripNumberRequired = Error.Validation(
        "Trips.Trip.TripNumberRequired", "A trip number is required.");

    public static readonly Error RouteNameRequired = Error.Validation(
        "Trips.Trip.RouteNameRequired", "A route name is required.");

    public static readonly Error OriginAndDestinationRequired = Error.Validation(
        "Trips.Trip.OriginAndDestinationRequired", "An origin and a destination are required.");

    public static readonly Error InvalidDistance = Error.Validation(
        "Trips.Trip.InvalidDistance", "The trip distance cannot be negative.");

    public static readonly Error InvalidSeats = Error.Validation(
        "Trips.Trip.InvalidSeats", "Seat counts cannot be negative.");

    public static readonly Error SeatsExceedCapacity = Error.Validation(
        "Trips.Trip.SeatsExceedCapacity", "Confirmed seats cannot exceed the trip's seat capacity.");

    public static readonly Error VehicleCapacityBelowConfirmed = Error.Validation(
        "Trips.Trip.VehicleCapacityBelowConfirmed", "The vehicle's seating capacity is below the seats already confirmed on this trip.");

    public static readonly Error DriverRequired = Error.Validation(
        "Trips.Trip.DriverRequired", "A trip cannot be created without an assigned driver.");

    public static readonly Error VehicleRequired = Error.Validation(
        "Trips.Trip.VehicleRequired", "A trip cannot be created without an assigned fleet vehicle.");

    public static readonly Error DriverNameRequired = Error.Validation(
        "Trips.Trip.DriverNameRequired", "Assigning a driver requires the driver's name snapshot.");

    public static readonly Error VehicleUnitRequired = Error.Validation(
        "Trips.Trip.VehicleUnitRequired", "Assigning a vehicle requires the vehicle's unit-number snapshot.");

    public static readonly Error DriverNotFound = Error.NotFound(
        "Trips.Trip.DriverNotFound", "The driver to assign was not found.");

    public static readonly Error DriverNotActive = Error.Validation(
        "Trips.Trip.DriverNotActive", "Only an active driver can be assigned to a trip.");

    public static readonly Error VehicleNotFound = Error.NotFound(
        "Trips.Trip.VehicleNotFound", "The vehicle to assign was not found.");

    public static readonly Error VehicleNotActive = Error.Validation(
        "Trips.Trip.VehicleNotActive", "Only an active vehicle can be assigned to a trip.");

    public static readonly Error NotEditable = Error.Conflict(
        "Trips.Trip.NotEditable", "Only a scheduled trip can be edited.");

    public static readonly Error ManifestAlreadyAttached = Error.Conflict(
        "Trips.Trip.ManifestAlreadyAttached", "A different manifest is already attached to this trip.");

    public static readonly Error PassengerManifestRequired = Error.Conflict(
        "Trips.Trip.PassengerManifestRequired", "A trip cannot go en route until its manifest has at least one passenger.");

    public static readonly Error DemandNotApplicable = Error.Conflict(
        "Trips.Trip.DemandNotApplicable", "A cargo trip carries goods, not passengers — seat demand does not apply.");

    public static readonly Error ShipmentRequired = Error.Conflict(
        "Trips.Trip.ShipmentRequired", "A cargo trip cannot go en route until at least one shipment is assigned.");

    public static readonly Error ManifestNotAllowedForEmptyLeg = Error.Conflict(
        "Trips.Trip.ManifestNotAllowedForEmptyLeg", "A deadhead trip carries no passengers — a manifest cannot be created for it.");

    public static readonly Error PostTripInspectionRequired = Error.Conflict(
        "Trips.Trip.PostTripInspectionRequired", "A post-trip inspection must be logged before this trip's run can be closed out.");

    public static readonly Error WriteOffReasonRequired = Error.Validation(
        "Trips.Trip.WriteOffReasonRequired", "Closing a trip without billing requires a reason.");

    public static readonly Error OnWorksheetCannotCloseWithoutBilling = Error.Conflict(
        "Trips.Trip.OnWorksheetCannotCloseWithoutBilling",
        "A worksheet already claims this trip — void the draft (or write the invoice off) instead of closing the trip without billing.");

    public static readonly Error BillingStateOnClientlessTrip = Error.Conflict(
        "Trips.Trip.BillingStateOnClientlessTrip", "A run with no client never enters the billing arc, so no invoice can speak for it.");

    public static readonly Error FinishIsItsOwnCommand = Error.Conflict(
        "Trips.Trip.FinishIsItsOwnCommand",
        "Recording that a run is over goes through POST /api/trips/{id}/finish — the resulting status depends on whether the trip has a client.");

    public static readonly Error InvalidStatusFilter = Error.Validation(
        "Trips.Trip.InvalidStatusFilter", "The status filter is not a known trip status.");

    public static readonly Error RoundTripSameTrip = Error.Validation(
        "Trips.Trip.RoundTripSameTrip", "A trip cannot be merged with itself.");

    public static readonly Error RoundTripTenantMismatch = Error.Validation(
        "Trips.Trip.RoundTripTenantMismatch", "Both trips of a round trip must belong to the same tenant.");

    public static readonly Error RoundTripClientRequired = Error.Validation(
        "Trips.Trip.RoundTripClientRequired", "Round-trip pairing requires a client on the trip — community and unattached runs are excluded.");

    public static readonly Error RoundTripClientMismatch = Error.Validation(
        "Trips.Trip.RoundTripClientMismatch", "Both trips of a round trip must belong to the same client.");

    public static readonly Error RoundTripServiceDateMismatch = Error.Validation(
        "Trips.Trip.RoundTripServiceDateMismatch", "Both trips of a round trip must run on the same service date.");

    public static readonly Error RoundTripCorridorMismatch = Error.Validation(
        "Trips.Trip.RoundTripCorridorMismatch", "The trips' corridors must mirror each other — one trip's origin must be the other's destination.");

    public static readonly Error RoundTripAlreadyPaired = Error.Conflict(
        "Trips.Trip.RoundTripAlreadyPaired", "The trip already belongs to a round trip.");

    public static readonly Error RoundTripFinal = Error.Conflict(
        "Trips.Trip.RoundTripFinal", "A cancelled or written-off trip cannot take part in a round trip.");

    public static readonly Error RoundTripNotPaired = Error.Conflict(
        "Trips.Trip.RoundTripNotPaired", "The trip does not belong to a round trip.");

    public static readonly Error RoundTripManifestDirectionConflict = Error.Conflict(
        "Trips.Trip.RoundTripManifestDirectionConflict",
        "Both trips' manifests declare the same leg direction — correct one manifest before merging.");

    public static readonly Error RoundTripKeyRequired = Error.Validation(
        "Trips.Trip.RoundTripKeyRequired", "A round-trip key is required to pair trips.");

    public static readonly Error BookingDayRequired = Error.Validation(
        "Trips.Trip.BookingDayRequired", "A booking-sourced trip requires the booking day it came from.");

    public static readonly Error DeadheadReturnOfEmptyLeg = Error.Conflict(
        "Trips.Trip.DeadheadReturnOfEmptyLeg", "An empty leg cannot get a deadhead return of its own.");

    public static readonly Error UseChangeRoute = Error.Conflict(
        "Trips.Trip.UseChangeRoute",
        "A trip's route is changed through POST /api/trips/{id}/change-route, which also moves a still-scheduled paired leg — an edit can only keep the current route.");

    public static readonly Error RouteChangeNotScheduled = Error.Conflict(
        "Trips.Trip.RouteChangeNotScheduled", "Only a scheduled trip's route can be changed — this run has already started or ended.");

    public static readonly Error RouteOwnedByBooking = Error.Conflict(
        "Trips.Trip.RouteOwnedByBooking",
        "This trip was confirmed from a community booking day, so its route belongs to the booking — change the booking instead.");

    public static readonly Error RouteUnchanged = Error.Conflict(
        "Trips.Trip.RouteUnchanged", "The trip already runs on this route.");

    public static readonly Error RouteInactive = Error.Validation(
        "Trips.Trip.RouteInactive", "An inactive route cannot be given to a trip.");

    public static readonly Error RouteChangeNeedsAcknowledgement = Error.Conflict(
        "Trips.Trip.RouteChangeNeedsAcknowledgement",
        "Changing the route affects passengers, cargo, or an imported booking — review the warnings and confirm to proceed.");

    public static readonly Error ChangedConcurrently = Error.Conflict(
        "Trips.Trip.ChangedConcurrently", "Someone else changed this trip at the same time — reload it and try again.");

    public static readonly Error DeadheadConversionNotScheduled = Error.Conflict(
        "Trips.Trip.DeadheadConversionNotScheduled",
        "Only a scheduled trip can be converted to a deadhead — this run has already started or ended.");

    public static readonly Error AlreadyEmptyLeg = Error.Conflict(
        "Trips.Trip.AlreadyEmptyLeg", "The trip is already a deadhead.");

    public static readonly Error DeadheadConversionBookingSourced = Error.Conflict(
        "Trips.Trip.DeadheadConversionBookingSourced",
        "This trip was confirmed from a community booking day — its passengers are booked in Community Booking, so it cannot become a deadhead.");

    public static readonly Error PassengerTripConversionNotScheduled = Error.Conflict(
        "Trips.Trip.PassengerTripConversionNotScheduled",
        "Only a scheduled deadhead can be converted back — this run has already started or ended.");

    public static readonly Error NotEmptyLeg = Error.Conflict(
        "Trips.Trip.NotEmptyLeg", "The trip is not a deadhead.");

    public static Error DeadheadConversionHasDemand(int seatsConfirmed, bool demandGuaranteed) => Error.Conflict(
        "Trips.Trip.DeadheadConversionHasDemand",
        demandGuaranteed
            ? $"This trip has a gift-a-seat pledge{(seatsConfirmed > 0 ? $" and {seatsConfirmed} confirmed seat{(seatsConfirmed == 1 ? string.Empty : "s")}" : string.Empty)} — clear its demand before converting it to a deadhead."
            : $"This trip has {seatsConfirmed} confirmed seat{(seatsConfirmed == 1 ? string.Empty : "s")} — clear its demand before converting it to a deadhead.");

    public static Error DeadheadConversionManifestNotEmpty(int passengers, int cargoItems) => Error.Conflict(
        "Trips.Trip.DeadheadConversionManifestNotEmpty",
        $"This trip's manifest still lists {passengers} passenger{(passengers == 1 ? string.Empty : "s")} and {cargoItems} cargo item{(cargoItems == 1 ? string.Empty : "s")} — empty it before converting the trip to a deadhead.");

    public static Error DeadheadConversionHasExternalBookings(int bookings) => Error.Conflict(
        "Trips.Trip.DeadheadConversionHasExternalBookings",
        $"{bookings} imported Bookeo booking{(bookings == 1 ? " is" : "s are")} placed on this trip — cancel or move {(bookings == 1 ? "it" : "them")} in Bookeo and re-import before converting it to a deadhead.");

    public static Error RoundTripBothLegsEmpty(string partnerTripNumber) => Error.Conflict(
        "Trips.Trip.RoundTripBothLegsEmpty",
        $"The paired leg {partnerTripNumber} is already a deadhead — both legs of a round trip cannot run empty. Unpair them first.");

    public static Error RouteChangePartnerOwnedByBooking(string partnerTripNumber) => Error.Conflict(
        "Trips.Trip.RouteOwnedByBooking",
        $"The paired leg {partnerTripNumber} was confirmed from a community booking day, so its route belongs to the booking.");

    public static Error RouteChangeCargoUnderway(string tripNumber) => Error.Conflict(
        "Trips.Trip.RouteChangeCargoUnderway",
        $"Freight has already been picked up or dropped on {tripNumber}, so its route can no longer change.");

    public static Error InvalidStatusTransition(TripStatus from, TripStatus to) => Error.Conflict(
        "Trips.Trip.InvalidStatusTransition", $"A trip cannot move from {from} to {to}.");

    public static Error OperationallyClosed(TripStatus status) => Error.Conflict(
        "Trips.Trip.OperationallyClosed",
        $"The run has already happened — a {status} trip's driver, vehicle, and demand can no longer be changed.");

    public static Error SystemDrivenStatus(TripStatus status) => Error.Conflict(
        "Trips.Trip.SystemDrivenStatus",
        $"{status} is set by Billing when the worksheet is entered in QuickBooks, paid, or written off — it cannot be set by hand.");
}
