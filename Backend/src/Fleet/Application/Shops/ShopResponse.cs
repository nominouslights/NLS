namespace NorthernLink.Fleet.Application.Shops;

/// <summary>The Fleet module's public representation of a shop / parts partner.</summary>
public sealed record ShopResponse(
    Guid Id,
    string Number,
    string Name,
    string? ContactName,
    string? Phone,
    string? Email,
    string? Address,
    string? GstBusinessNo,
    bool MpiAccredited,
    string? InspectionStationNo,
    bool SuppliesParts,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
