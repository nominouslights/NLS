using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.CostCentres;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// In-memory fake of the cost-centre register's write side. Every read is scoped to
/// <see cref="QueryFilterTenantId"/>, modelling the tenant query filter, and the code lookup is
/// ordinal like the real repository — a case-insensitive fake would hide a normalization bug.
/// </summary>
internal sealed class InMemoryCostCentreRepository : ICostCentreRepository
{
    public List<CostCentre> CostCentres { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public Guid QueryFilterTenantId { get; set; } = TestBudgeting.TenantId;

    private IEnumerable<CostCentre> Visible => CostCentres.Where(c => c.TenantId == QueryFilterTenantId);

    private readonly List<CostCentre> _pendingAdds = [];

    /// <summary>
    /// When true, the next <see cref="TrySaveChangesAsync"/> simulates losing the race on the
    /// unique (tenant_id, code) index: it persists nothing (pending adds are dropped, as the real
    /// repository's cleared tracker would), lets <see cref="RaceWinner"/> appear as the row that
    /// won, and returns false. One-shot.
    /// </summary>
    public bool UniqueCodeViolationOnNextSave { get; set; }

    /// <summary>The concurrently saved entry that holds the code, if any.</summary>
    public CostCentre? RaceWinner { get; set; }

    /// <summary>The tenant of every register lock taken, in order.</summary>
    public List<Guid> Locks { get; } = [];

    /// <summary>The tenant of every lock scope that was committed, in order.</summary>
    public List<Guid> CommittedLocks { get; } = [];

    /// <summary>
    /// Every repository call in order — <c>Lock</c>, <c>GetById</c>, <c>GetByCode</c>,
    /// <c>HasChildren</c>, <c>HasActiveChildren</c>, <c>Save</c>, <c>Commit</c> — so a test can
    /// pin that the register lock comes before the first register read.
    /// </summary>
    public List<string> Calls { get; } = [];

    /// <summary>
    /// Runs as each lock is acquired: models a competing write that committed just before this
    /// request got the lock (the waiter then sees it).
    /// </summary>
    public Action? OnLockAcquired { get; set; }

    /// <summary>
    /// The async form of <see cref="OnLockAcquired"/>: lets a test run a whole competing handler
    /// to completion "while this request waited on the lock". Clear it inside the callback, or
    /// the competing handler's own lock acquisition re-enters it.
    /// </summary>
    public Func<Task>? OnLockAcquiredAsync { get; set; }

    public Task<CostCentre?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Calls.Add("GetById");
        return Task.FromResult(Visible.FirstOrDefault(c => c.Id == id));
    }

    public Task<CostCentre?> GetByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default)
    {
        Calls.Add("GetByCode");
        return Task.FromResult(Visible.FirstOrDefault(c => string.Equals(c.Code, normalizedCode, StringComparison.Ordinal)));
    }

    public Task<bool> HasChildrenAsync(Guid parentId, CancellationToken cancellationToken = default)
    {
        Calls.Add("HasChildren");
        return Task.FromResult(Visible.Any(c => c.ParentId == parentId));
    }

    public Task<bool> HasActiveChildrenAsync(Guid parentId, CancellationToken cancellationToken = default)
    {
        Calls.Add("HasActiveChildren");
        return Task.FromResult(Visible.Any(c => c.ParentId == parentId && c.IsActive));
    }

    public void Add(CostCentre costCentre)
    {
        CostCentres.Add(costCentre);
        _pendingAdds.Add(costCentre);
    }

    public void Remove(CostCentre costCentre) => CostCentres.Remove(costCentre);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add("Save");
        SaveChangesCallCount++;
        _pendingAdds.Clear();
        return Task.CompletedTask;
    }

    public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add("Save");
        SaveChangesCallCount++; // a save attempt either way, like the real SaveChanges round trip

        if (!UniqueCodeViolationOnNextSave)
        {
            _pendingAdds.Clear();
            return Task.FromResult(true);
        }

        UniqueCodeViolationOnNextSave = false;
        foreach (var pending in _pendingAdds)
        {
            CostCentres.Remove(pending);
        }

        _pendingAdds.Clear();
        if (RaceWinner is not null)
        {
            CostCentres.Add(RaceWinner);
        }

        return Task.FromResult(false);
    }

    public async Task<ICostCentreRegisterLock> LockRegisterAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        Calls.Add("Lock");
        Locks.Add(tenantId);
        OnLockAcquired?.Invoke();
        if (OnLockAcquiredAsync is { } competing)
        {
            await competing();
        }

        return new RecordingLock(this, tenantId);
    }

    private sealed class RecordingLock(InMemoryCostCentreRepository owner, Guid tenantId) : ICostCentreRegisterLock
    {
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            owner.Calls.Add("Commit");
            owner.CommittedLocks.Add(tenantId);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
