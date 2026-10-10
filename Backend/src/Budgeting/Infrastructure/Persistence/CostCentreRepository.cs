using Microsoft.EntityFrameworkCore;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.CostCentres;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="BudgetingDbContext"/> (tenant-filtered).</summary>
internal sealed class CostCentreRepository(BudgetingDbContext context) : ICostCentreRepository
{
    public Task<CostCentre?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.CostCentres.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    // Ordinal equality on the already-trimmed code — a seek on the unique (tenant_id, code) index.
    public Task<CostCentre?> GetByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default) =>
        context.CostCentres.FirstOrDefaultAsync(c => c.Code == normalizedCode, cancellationToken);

    public Task<bool> HasChildrenAsync(Guid parentId, CancellationToken cancellationToken = default) =>
        context.CostCentres.AnyAsync(c => c.ParentId == parentId, cancellationToken);

    public Task<bool> HasActiveChildrenAsync(Guid parentId, CancellationToken cancellationToken = default) =>
        context.CostCentres.AnyAsync(c => c.ParentId == parentId && c.IsActive, cancellationToken);

    public void Add(CostCentre costCentre) => context.CostCentres.Add(costCentre);

    public void Remove(CostCentre costCentre) => context.CostCentres.Remove(costCentre);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
