using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Shops;

/// <summary>All domain errors the Shop aggregate (and its handlers) can produce.</summary>
public static class ShopErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Fleet.Shop.NotFound", "The shop was not found.");

    public static readonly Error NameRequired = Error.Validation(
        "Fleet.Shop.NameRequired", "A shop or partner name is required.");
}
