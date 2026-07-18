using NorthernLink.Fleet.Domain.Shops;

namespace NorthernLink.Fleet.Application.Shops;

/// <summary>Maps the Shop aggregate to its public response contract.</summary>
public static class ShopResponseMapper
{
    public static ShopResponse ToResponse(Shop shop) => new(
        shop.Id,
        shop.Number,
        shop.Name,
        shop.ContactName,
        shop.Phone,
        shop.Email,
        shop.Address,
        shop.GstBusinessNo,
        shop.MpiAccredited,
        shop.InspectionStationNo,
        shop.SuppliesParts,
        shop.Notes,
        shop.CreatedAtUtc,
        shop.UpdatedAtUtc);
}
