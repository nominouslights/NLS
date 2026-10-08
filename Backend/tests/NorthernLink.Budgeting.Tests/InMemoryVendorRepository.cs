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

    private readonly List<Vendor> _pendingAdds = [];

    /// <summary>
    /// When true, the next <see cref="TrySaveChangesAsync"/> simulates losing the race on the
    /// unique (tenant_id, normalized_name) index: it persists nothing (pending adds are dropped,
    /// as the real repository's cleared tracker would), lets <see cref="RaceWinner"/> appear as
    /// the row that won, and returns false. One-shot.
    /// </summary>
    public bool UniqueNameViolationOnNextSave { get; set; }

    /// <summary>The concurrently saved vendor that holds the name, or null to model a winner
    /// that can no longer be read back (renamed or deleted since).</summary>
    public Vendor? RaceWinner { get; set; }

    public void Add(Vendor vendor)
    {
        Vendors.Add(vendor);
        _pendingAdds.Add(vendor);
    }

    public void Remove(Vendor vendor) => Vendors.Remove(vendor);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        _pendingAdds.Clear();
        return Task.CompletedTask;
    }

    public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++; // a save attempt either way, like the real SaveChanges round trip

        if (!UniqueNameViolationOnNextSave)
        {
            _pendingAdds.Clear();
            return Task.FromResult(true);
        }

        UniqueNameViolationOnNextSave = false;
        foreach (var pending in _pendingAdds)
        {
            Vendors.Remove(pending);
        }

        _pendingAdds.Clear();

        // First in the list so a name lookup finds the winner ahead of an in-memory rename of
        // the loser (the real tracker clear reverts that rename; this fake cannot).
        if (RaceWinner is not null)
        {
            Vendors.Insert(0, RaceWinner);
        }

        return Task.FromResult(false);
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
