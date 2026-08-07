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

    public static readonly Error DriverNameRequired = Error.Validation(
        "Trips.Trip.DriverNameRequired", "Assigning a driver requires the driver's name snapshot.");

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

    public static readonly Error DeadheadReturnOfEmptyLeg = Error.Conflict(
        "Trips.Trip.DeadheadReturnOfEmptyLeg", "An empty leg cannot get a deadhead return of its own.");

    public static Error InvalidStatusTransition(TripStatus from, TripStatus to) => Error.Conflict(
        "Trips.Trip.InvalidStatusTransition", $"A trip cannot move from {from} to {to}.");

    public static Error OperationallyClosed(TripStatus status) => Error.Conflict(
        "Trips.Trip.OperationallyClosed",
        $"The run has already happened — a {status} trip's driver, vehicle, and demand can no longer be changed.");

    public static Error SystemDrivenStatus(TripStatus status) => Error.Conflict(
        "Trips.Trip.SystemDrivenStatus",
        $"{status} is set by Billing when the worksheet is entered in QuickBooks, paid, or written off — it cannot be set by hand.");
}
