using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Shops.Update;

/// <summary>Updates an existing shop / parts partner's details.</summary>
public sealed record UpdateShopCommand(
    Guid TenantId,
    Guid ShopId,
    string Name,
    string? ContactName,
    string? Phone,
    string? Email,
    string? Address,
    string? GstBusinessNo,
    bool MpiAccredited,
    string? InspectionStationNo,
    bool SuppliesParts,
    string? Notes) : ICommand;
