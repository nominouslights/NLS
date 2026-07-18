using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Manifests;

/// <summary>All domain errors the TripManifest aggregate (and its handlers) can produce.</summary>
public static class TripManifestErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Trips.Manifest.NotFound", "The trip manifest was not found.");

    public static readonly Error TripNumberRequired = Error.Validation(
        "Trips.Manifest.TripNumberRequired", "A trip number is required.");

    public static readonly Error UnitRequired = Error.Validation(
        "Trips.Manifest.UnitRequired", "The vehicle unit is required.");

    public static readonly Error DriverNameRequired = Error.Validation(
        "Trips.Manifest.DriverNameRequired", "The driver's name is required.");

    public static readonly Error TooManyPassengers = Error.Validation(
        "Trips.Manifest.TooManyPassengers", $"The manifest holds at most {ManifestChecklist.MaxPassengers} passengers.");

    public static readonly Error TooManyCargoItems = Error.Validation(
        "Trips.Manifest.TooManyCargoItems", $"The manifest holds at most {ManifestChecklist.MaxCargoItems} cargo items.");

    public static readonly Error FailRequiresNote = Error.Validation(
        "Trips.Manifest.FailRequiresNote", "Every failed pre-trip item requires a note describing the defect.");

    public static readonly Error OdometerEndBeforeStart = Error.Validation(
        "Trips.Manifest.OdometerEndBeforeStart", "The end odometer reading cannot be lower than the start reading.");

    public static readonly Error FuelLitresRequired = Error.Validation(
        "Trips.Manifest.FuelLitresRequired", "Fuel litres are required when fuel was added.");

    public static readonly Error AttestationsIncomplete = Error.Validation(
        "Trips.Manifest.AttestationsIncomplete", $"All {ManifestChecklist.AttestationCount} certification attestations must be confirmed.");

    public static readonly Error SignatureRequired = Error.Validation(
        "Trips.Manifest.SignatureRequired", "The driver's signature name is required to certify the manifest.");

    public static readonly Error EnteredByRequired = Error.Validation(
        "Trips.Manifest.EnteredByRequired", "A paper-transcribed manifest must record who entered it.");
}
