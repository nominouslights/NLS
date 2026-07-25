using NorthernLink.Fleet.Application.Shops;

namespace NorthernLink.Fleet.Application.Abstractions;

/// <summary>Read side for shop queries — returns response DTOs directly (tenant-scoped).</summary>
public interface IShopReadService
{
    Task<IReadOnlyList<ShopResponse>> GetShopsAsync(CancellationToken cancellationToken = default);
}
