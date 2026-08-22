using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Manifests;

/// <summary>All domain errors the TripManifest aggregate (and its handlers) can produce.</summary>
public static class TripManifestErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Trips.Manifest.NotFound", "The trip manifest was not found.");

    public static readonly Error TripNumberRequired = Error.Validation(
        "Trips.Manifest.TripNumberRequired", "A trip number is required.");

    public static readonly Error TooManyPassengers = Error.Validation(
        "Trips.Manifest.TooManyPassengers", $"The manifest holds at most {ManifestChecklist.MaxPassengers} passengers.");

    public static readonly Error TooManyCargoItems = Error.Validation(
        "Trips.Manifest.TooManyCargoItems", $"The manifest holds at most {ManifestChecklist.MaxCargoItems} cargo items.");

    public static readonly Error PassengerNameRequired = Error.Validation(
        "Trips.Manifest.PassengerNameRequired", "Every passenger row requires a name.");

    public static readonly Error EnteredByRequired = Error.Validation(
        "Trips.Manifest.EnteredByRequired", "A dispatcher-entered manifest must record who entered it.");

    public static readonly Error InvalidFareAmount = Error.Validation(
        "Trips.Manifest.InvalidFareAmount", "A fare cannot be negative and is recorded to the cent.");

    public static readonly Error FarePaymentMethodRequired = Error.Validation(
        "Trips.Manifest.FarePaymentMethodRequired", "Recording a fare requires how it was paid — cash, online, or waived.");

    public static readonly Error FareAmountRequired = Error.Validation(
        "Trips.Manifest.FareAmountRequired", "A cash or online fare needs an amount.");

    public static readonly Error WaivedFareMustBeZero = Error.Validation(
        "Trips.Manifest.WaivedFareMustBeZero", "A waived fare cannot also carry an amount.");
}
