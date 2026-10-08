using Microsoft.EntityFrameworkCore;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Vendors;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="BudgetingDbContext"/> (tenant-filtered).</summary>
internal sealed class VendorRepository(BudgetingDbContext context) : IVendorRepository
{
    public Task<Vendor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Vendors.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    // Ordinal equality on an already-normalized string — a seek on the unique
    // (tenant_id, normalized_name) index rather than a case-insensitive scan.
    public Task<Vendor?> GetByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default) =>
        context.Vendors.FirstOrDefaultAsync(v => v.NormalizedName == normalizedName, cancellationToken);

    public void Add(Vendor vendor) => context.Vendors.Add(vendor);

    public void Remove(Vendor vendor) => context.Vendors.Remove(vendor);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
