using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Shops.Register;

/// <summary>Registers a new shop / parts partner. Returns the new shop's id.</summary>
public sealed record RegisterShopCommand(
    Guid TenantId,
    string Name,
    string? ContactName,
    string? Phone,
    string? Email,
    string? Address,
    string? GstBusinessNo,
    bool MpiAccredited,
    string? InspectionStationNo,
    bool SuppliesParts,
    string? Notes) : ICommand<Guid>;
