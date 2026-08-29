using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Riders;

/// <summary>All domain errors the Rider aggregate (and its handlers) can produce.</summary>
public static class RiderErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Trips.Rider.NotFound", "The rider was not found.");

    public static readonly Error NameRequired = Error.Validation(
        "Trips.Rider.NameRequired", "A rider name is required.");

    public static readonly Error RotationNotApplicable = Error.Validation(
        "Trips.Rider.RotationNotApplicable", "Only contract-crew riders carry a rotation.");

    public static readonly Error InvalidRotation = Error.Validation(
        "Trips.Rider.InvalidRotation", "Rotation must be 5, 10, or 20 days.");
}
