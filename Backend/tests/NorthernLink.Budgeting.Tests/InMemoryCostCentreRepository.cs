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

    public Task<CostCentre?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Visible.FirstOrDefault(c => c.Id == id));

    public Task<CostCentre?> GetByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(Visible.FirstOrDefault(c => string.Equals(c.Code, normalizedCode, StringComparison.Ordinal)));

    public Task<bool> HasChildrenAsync(Guid parentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Visible.Any(c => c.ParentId == parentId));

    public Task<bool> HasActiveChildrenAsync(Guid parentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Visible.Any(c => c.ParentId == parentId && c.IsActive));

    public void Add(CostCentre costCentre) => CostCentres.Add(costCentre);

    public void Remove(CostCentre costCentre) => CostCentres.Remove(costCentre);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
