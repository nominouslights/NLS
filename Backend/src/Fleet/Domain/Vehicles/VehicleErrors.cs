using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Vehicles;

/// <summary>All domain errors the Vehicle aggregate (and its handlers) can produce.</summary>
public static class VehicleErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Fleet.Vehicle.NotFound", "The vehicle was not found.");

    public static readonly Error DuplicateVin = Error.Conflict(
        "Fleet.Vehicle.DuplicateVin", "A vehicle with this VIN is already registered for this tenant.");

    public static readonly Error DuplicateUnitNumber = Error.Conflict(
        "Fleet.Vehicle.DuplicateUnitNumber", "A vehicle with this unit number already exists for this tenant.");

    public static readonly Error InvalidVin = Error.Validation(
        "Fleet.Vehicle.InvalidVin", "A VIN must be exactly 17 uppercase alphanumeric characters and may not contain I, O, or Q.");

    public static readonly Error InvalidUnitNumber = Error.Validation(
        "Fleet.Vehicle.InvalidUnitNumber", "A unit number is required.");

    public static readonly Error InvalidCapacity = Error.Validation(
        "Fleet.Vehicle.InvalidCapacity", "Seating capacity must be greater than zero.");

    public static readonly Error InvalidYear = Error.Validation(
        "Fleet.Vehicle.InvalidYear", "The model year is not plausible.");

    public static readonly Error InvalidOdometer = Error.Validation(
        "Fleet.Vehicle.InvalidOdometer", "The odometer reading cannot be negative.");

    public static readonly Error InvalidAcquisitionCost = Error.Validation(
        "Fleet.Vehicle.InvalidAcquisitionCost", "The acquisition cost cannot be negative.");

    public static readonly Error InvalidEndOfLifeKm = Error.Validation(
        "Fleet.Vehicle.InvalidEndOfLifeKm", "The end-of-life kilometre threshold must be greater than zero.");

    public static readonly Error InvalidSalePrice = Error.Validation(
        "Fleet.Vehicle.InvalidSalePrice", "The sale price cannot be negative.");

    public static Error InvalidStatusTransition(VehicleStatus from, VehicleStatus to) => Error.Conflict(
        "Fleet.Vehicle.InvalidStatusTransition", $"A vehicle cannot move from {from} to {to}.");

    public static readonly Error StatusReasonRequired = Error.Validation(
        "Fleet.Vehicle.StatusReasonRequired", "Taking a vehicle out of service requires a reason.");

    public static readonly Error Disposed = Error.Conflict(
        "Fleet.Vehicle.Disposed", "The vehicle has been disposed (sold or recycled) and can no longer change.");

    public static readonly Error NotRetired = Error.Validation(
        "Fleet.Vehicle.NotRetired", "A vehicle must be retired before it can be sold or recycled.");

    public static readonly Error OdometerRollback = Error.Validation(
        "Fleet.Vehicle.OdometerRollback", "An odometer reading cannot be lower than the current reading.");

    public static readonly Error CertificateNotFound = Error.NotFound(
        "Fleet.Vehicle.CertificateNotFound", "No retirement certificate exists for this vehicle.");
}
