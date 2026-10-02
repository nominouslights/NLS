using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Domain.BookeoImports;

/// <summary>
/// Maps a Bookeo product (by its stable <see cref="ProductCode"/>, and for bidirectional products
/// also the row's <see cref="Destination"/>) onto a Trips route and direction. A null destination
/// matches any row of the product; an exact destination match wins over it. Unique per tenant on
/// (product_code, lower(coalesce(destination, ''))).
/// </summary>
public sealed class BookeoProductMapping : Entity, ITenantScoped
{
    private BookeoProductMapping()
    {
        ProductCode = null!;
        ProductName = null!;
    }

    public Guid TenantId { get; private set; }
    public string ProductCode { get; private set; }

    /// <summary>Display only — the code is the key.</summary>
    public string ProductName { get; private set; }

    public string? Destination { get; private set; }
    public Guid RouteId { get; private set; }
    public TripDirection? Direction { get; private set; }
    public ResidentStopRole ResidentStopRole { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Result<BookeoProductMapping> Create(
        Guid tenantId,
        string? productCode,
        string? productName,
        string? destination,
        Guid routeId,
        TripDirection? direction,
        ResidentStopRole residentStopRole)
    {
        var mapping = new BookeoProductMapping { TenantId = tenantId };
        var result = mapping.Update(productCode, productName, destination, routeId, direction, residentStopRole);
        return result.IsFailure ? Result.Failure<BookeoProductMapping>(result.Error) : Result.Success(mapping);
    }

    public Result Update(
        string? productCode,
        string? productName,
        string? destination,
        Guid routeId,
        TripDirection? direction,
        ResidentStopRole residentStopRole)
    {
        var code = BookeoText.Clean(productCode);
        if (code is null)
        {
            return Result.Failure(BookeoImportErrors.ProductCodeRequired);
        }

        if (routeId == Guid.Empty)
        {
            return Result.Failure(BookeoImportErrors.RouteRequired);
        }

        ProductCode = code;
        ProductName = BookeoText.Clean(productName) ?? code;
        Destination = BookeoText.NormalizeDestination(destination);
        RouteId = routeId;
        Direction = direction;
        ResidentStopRole = residentStopRole;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    /// <summary>Whether this mapping is the one for (code, destination) — the upsert key.</summary>
    public bool HasKey(string? productCode, string? destination) =>
        string.Equals(ProductCode, BookeoText.Clean(productCode), StringComparison.Ordinal)
        && BookeoText.SameDestination(Destination, destination);
}
