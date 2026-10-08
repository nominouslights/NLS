using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Vendors;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// In-memory fake of the write-side vendor repository. Every read is scoped to
/// <see cref="QueryFilterTenantId"/>, modelling the tenant query filter, so "another tenant's
/// vendor reads as not found" is exercised rather than assumed.
/// </summary>
internal sealed class InMemoryVendorRepository : IVendorRepository
{
    public List<Vendor> Vendors { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    /// <summary>The ambient tenant the real DbContext's query filter would compare against.</summary>
    public Guid QueryFilterTenantId { get; set; } = TestBudgeting.TenantId;

    private IEnumerable<Vendor> Visible => Vendors.Where(v => v.TenantId == QueryFilterTenantId);

    public Task<Vendor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Visible.FirstOrDefault(v => v.Id == id));

    // Ordinal, matching the real repository: the handler normalizes before calling, so a
    // case-insensitive comparison here would hide a missing normalization.
    public Task<Vendor?> GetByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default) =>
        Task.FromResult(Visible.FirstOrDefault(v =>
            string.Equals(v.NormalizedName, normalizedName, StringComparison.Ordinal)));

    public void Add(Vendor vendor) => Vendors.Add(vendor);

    public void Remove(Vendor vendor) => Vendors.Remove(vendor);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}

/// <summary>Usage probe whose answer the test chooses, so the delete handler's 409 path is pinned today.</summary>
internal sealed class StubVendorUsageProbe : IVendorUsageProbe
{
    public bool Referenced { get; set; }

    public Guid? LastProbedId { get; private set; }

    public Task<bool> IsReferencedAsync(Guid vendorId, CancellationToken cancellationToken = default)
    {
        LastProbedId = vendorId;
        return Task.FromResult(Referenced);
    }
}
