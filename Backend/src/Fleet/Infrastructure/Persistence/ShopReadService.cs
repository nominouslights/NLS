using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Shops;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>Read side — queries the read model through fleet.v_shops and maps to the public contract.</summary>
internal sealed class ShopReadService(FleetDbContext context) : IShopReadService
{
    public async Task<IReadOnlyList<ShopResponse>> GetShopsAsync(CancellationToken cancellationToken = default)
    {
        var shops = await context.ShopReadModels
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        return shops.Select(s => new ShopResponse(
            s.Id,
            s.Number,
            s.Name,
            s.ContactName,
            s.Phone,
            s.Email,
            s.Address,
            s.GstBusinessNo,
            s.MpiAccredited,
            s.InspectionStationNo,
            s.SuppliesParts,
            s.Notes,
            s.CreatedAtUtc,
            s.UpdatedAtUtc)).ToList();
    }
}
