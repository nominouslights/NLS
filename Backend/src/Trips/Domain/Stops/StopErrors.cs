using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Stops;

/// <summary>All domain errors the Stop aggregate (and its value objects/handlers) can produce.</summary>
public static class StopErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Trips.Stop.NotFound", "The stop was not found.");

    public static readonly Error NameRequired = Error.Validation(
        "Trips.Stop.NameRequired", "A stop name is required.");

    public static readonly Error CityRequired = Error.Validation(
        "Trips.Stop.CityRequired", "The stop address needs a city.");

    public static readonly Error ProvinceRequired = Error.Validation(
        "Trips.Stop.ProvinceRequired", "The stop address needs a province.");

    public static readonly Error CountryRequired = Error.Validation(
        "Trips.Stop.CountryRequired", "The stop address needs a country.");

    public static readonly Error InvalidLatitude = Error.Validation(
        "Trips.Stop.InvalidLatitude", "Latitude must be between -90 and 90 degrees.");

    public static readonly Error InvalidLongitude = Error.Validation(
        "Trips.Stop.InvalidLongitude", "Longitude must be between -180 and 180 degrees.");
}
