using Microsoft.EntityFrameworkCore;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.CostCentres;
using NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;
using UserDisplay = NorthernLink.Budgeting.Application.CostCentres.CostCentrePlannedRollup.UserDisplay;

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

        var listed = all.Where(c => includeInactive || c.IsActive).ToList();
        var users = await LoadUsersAsync(
            listed.SelectMany(c => new[] { c.OwnerUserId, c.CreatedBy, c.ModifiedBy }), cancellationToken);
        var byId = all.ToDictionary(c => c.Id);

        return listed.Select(c => ToResponse(c, byId, users)).ToList();
    }

    public async Task<CostCentreResponse?> GetCostCentreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Two primary-key seeks (the entry, then its parent by id) rather than one OR'd query with
        // a correlated EXISTS, which the planner cannot answer from the primary key index.
        var costCentre = await context.CostCentreReadModels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (costCentre is null)
        {
            return null;
        }

        var byId = new Dictionary<Guid, CostCentreReadModel> { [costCentre.Id] = costCentre };
        if (costCentre.ParentId is { } parentId
            && await context.CostCentreReadModels
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == parentId, cancellationToken) is { } parent)
        {
            byId[parent.Id] = parent;
        }

        var users = await LoadUsersAsync(
            [costCentre.OwnerUserId, costCentre.CreatedBy, costCentre.ModifiedBy], cancellationToken);

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

        // The rollup displays only each register entry's owner.
        var users = await LoadUsersAsync(register.Select(e => e.OwnerUserId), cancellationToken);

        return CostCentrePlannedRollup.Build(periodId, items, codes, register, users);
    }

    /// <summary>
    /// The user_lookup rows for exactly the ids the caller will display (nulls dropped, distinct),
    /// as display values. No ids, no query.
    /// </summary>
    private async Task<Dictionary<Guid, UserDisplay>> LoadUsersAsync(
        IEnumerable<Guid?> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.OfType<Guid>().Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await context.UserLookups
            .AsNoTracking()
            .Where(u => ids.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => new UserDisplay(u.Email, u.FullName), cancellationToken);
    }

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

        var owner = Display(costCentre.OwnerUserId, users);
        var createdBy = Display(costCentre.CreatedBy, users);
        var modifiedBy = Display(costCentre.ModifiedBy, users);

        return new CostCentreResponse(
            costCentre.Id,
            costCentre.Code,
            costCentre.Name,
            costCentre.Description,
            costCentre.OwnerUserId,
            owner?.FullName,
            owner?.Email,
            costCentre.ParentId,
            parent?.Code,
            parent?.Name,
            costCentre.IsActive,
            costCentre.CreatedBy,
            createdBy?.FullName,
            createdBy?.Email,
            costCentre.ModifiedBy,
            modifiedBy?.FullName,
            modifiedBy?.Email,
            costCentre.CreatedAtUtc,
            costCentre.UpdatedAtUtc);
    }

    private static UserDisplay? Display(Guid? userId, IReadOnlyDictionary<Guid, UserDisplay> users) =>
        userId is { } id && users.TryGetValue(id, out var display) ? display : null;
}
