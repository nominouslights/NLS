using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Shops;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="FleetDbContext"/> (tenant-filtered).</summary>
internal sealed class ShopRepository(FleetDbContext context) : IShopRepository
{
    public Task<Shop?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Shops.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public void Add(Shop shop) => context.Shops.Add(shop);

    public async Task<int> NextSequenceAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // Count-based sequencing inside the unit of work; the unique index on
        // (tenant_id, number) guards against a concurrent duplicate.
        var existing = await context.Shops.CountAsync(s => s.TenantId == tenantId, cancellationToken);
        return existing + 1;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
