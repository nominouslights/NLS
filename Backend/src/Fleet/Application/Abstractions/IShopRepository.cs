using NorthernLink.Fleet.Domain.Shops;

namespace NorthernLink.Fleet.Application.Abstractions;

/// <summary>Write-side persistence for the Shop aggregate (tenant-scoped).</summary>
public interface IShopRepository
{
    Task<Shop?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(Shop shop);

    /// <summary>Next per-tenant sequence for SHOP-{seq} numbering.</summary>
    Task<int> NextSequenceAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
