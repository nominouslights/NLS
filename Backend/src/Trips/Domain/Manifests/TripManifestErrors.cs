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
}
