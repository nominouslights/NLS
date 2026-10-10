using Microsoft.EntityFrameworkCore;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.CostCentres;
using NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>
/// Read side — queries budgeting.rm_cost_centres and maps to the public contract, resolving the
/// parent and the three user ids to display values in memory (the
/// <see cref="BudgetCodeReadService"/> approach: a register is a handful of rows, and resolving at
/// read time means a display value is never stale).
/// <para>
/// The rollup reads the same projections the period totals read (<c>rm_budget_allocations</c>,
/// <c>rm_budget_codes</c>), classified by <see cref="CostCentrePlannedRollup"/>, so its total and
/// the period's <c>plannedExpenseCad</c> cannot disagree.
/// </para>
/// </summary>
internal sealed class CostCentreReadService(BudgetingDbContext context) : ICostCentreReadService
{
    public async Task<IReadOnlyList<CostCentreResponse>> GetCostCentresAsync(
        bool includeInactive, CancellationToken cancellationToken = default)
    {
        // The whole register, always: a parent may be retired while its child is listed, and the
        // child still needs its parent's code and name.
        var all = await context.CostCentreReadModels
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .ToListAsync(cancellationToken);

        if (all.Count == 0)
        {
            return [];
        }

        var users = await LoadUsersAsync(cancellationToken);
        var byId = all.ToDictionary(c => c.Id);

        return all
            .Where(c => includeInactive || c.IsActive)
            .Select(c => ToResponse(c, byId, users))
            .ToList();
    }

    public async Task<CostCentreResponse?> GetCostCentreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var costCentre = await context.CostCentreReadModels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (costCentre is null)
        {
            return null;
        }

        var byId = new Dictionary<Guid, CostCentreReadModel> { [costCentre.Id] = costCentre };
        if (costCentre.ParentId is { } parentId
            && await context.CostCentreReadModels.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == parentId, cancellationToken) is { } parent)
        {
            byId[parent.Id] = parent;
        }

        var users = await LoadUsersAsync(cancellationToken);
        return ToResponse(costCentre, byId, users);
    }

    public async Task<CostCentreRollupResponse> GetPlannedRollupAsync(
        Guid periodId, CancellationToken cancellationToken = default)
    {
        var items = await context.BudgetAllocationReadModels
            .AsNoTracking()
            .Where(a => a.PeriodId == periodId)
            .Select(a => new CostCentrePlannedRollup.ItemAmount(a.BudgetCodeId, a.AmountCad))
            .ToListAsync(cancellationToken);

        var codes = await context.BudgetCodeReadModels
            .AsNoTracking()
            .Where(c => c.PeriodId == periodId)
            .Select(c => new CostCentrePlannedRollup.PeriodCode(c.Id, c.Category, c.CostCentre))
            .ToListAsync(cancellationToken);

        var register = await context.CostCentreReadModels
            .AsNoTracking()
            .Select(c => new CostCentrePlannedRollup.RegisterEntry(
                c.Id, c.Code, c.Name, c.IsActive, c.ParentId, c.OwnerUserId))
            .ToListAsync(cancellationToken);

        var users = await context.UserLookups
            .AsNoTracking()
            .ToDictionaryAsync(
                u => u.UserId,
                u => new CostCentrePlannedRollup.UserDisplay(u.Email, u.FullName),
                cancellationToken);

        return CostCentrePlannedRollup.Build(periodId, items, codes, register, users);
    }

    private Task<Dictionary<Guid, UserDisplay>> LoadUsersAsync(CancellationToken cancellationToken) =>
        context.UserLookups
            .AsNoTracking()
            .ToDictionaryAsync(u => u.UserId, u => new UserDisplay(u.Email, u.FullName), cancellationToken);

    private readonly record struct UserDisplay(string Email, string? FullName);

    private static CostCentreResponse ToResponse(
        CostCentreReadModel costCentre,
        IReadOnlyDictionary<Guid, CostCentreReadModel> byId,
        IReadOnlyDictionary<Guid, UserDisplay> users)
    {
        // A parent id that resolves to nothing renders as null rather than throwing (the delete
        // guard refuses to orphan children, but a hand-written row should still list).
        var parent = costCentre.ParentId is { } parentId && byId.TryGetValue(parentId, out var found)
            ? found
            : null;

        return new CostCentreResponse(
            costCentre.Id,
            costCentre.Code,
            costCentre.Name,
            costCentre.Description,
            costCentre.OwnerUserId,
            Display(costCentre.OwnerUserId, users)?.FullName,
            Display(costCentre.OwnerUserId, users)?.Email,
            costCentre.ParentId,
            parent?.Code,
            parent?.Name,
            costCentre.IsActive,
            costCentre.CreatedBy,
            Display(costCentre.CreatedBy, users)?.FullName,
            Display(costCentre.CreatedBy, users)?.Email,
            costCentre.ModifiedBy,
            Display(costCentre.ModifiedBy, users)?.FullName,
            Display(costCentre.ModifiedBy, users)?.Email,
            costCentre.CreatedAtUtc,
            costCentre.UpdatedAtUtc);
    }

    private static UserDisplay? Display(Guid? userId, IReadOnlyDictionary<Guid, UserDisplay> users) =>
        userId is { } id && users.TryGetValue(id, out var display) ? display : null;
}
