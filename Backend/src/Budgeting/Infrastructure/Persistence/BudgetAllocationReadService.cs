using Microsoft.EntityFrameworkCore;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.Allocations;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>
/// Read side — queries budgeting.rm_budget_allocations for one period and maps to the public
/// contract, resolving each line's code to its current name, category, service line and active
/// flag, and each actor id to an email.
/// <para>
/// Three queries and two dictionaries, not a join — the <see cref="BudgetCodeReadService"/>
/// reasoning: the period's lines, the tenant's chart and the tenant's users are all small sets
/// already being materialized in full, and dictionary lookups read better than the SQL a
/// three-way GroupJoin/DefaultIfEmpty would produce. Resolving here rather than denormalizing
/// onto the read row is what keeps a re-classified code's lines in the right column.
/// </para>
/// <para>
/// A line whose code row is missing renders rather than throws: <c>Name = Code</c>,
/// <c>Category = Expense</c>, <c>IsCodeActive = false</c>. The usage probe refuses to delete a
/// referenced code, so this is a row written by hand or before that guard — it should still
/// list, and listing it as a retired expense line is the conservative reading.
/// </para>
/// </summary>
internal sealed class BudgetAllocationReadService(BudgetingDbContext context) : IBudgetAllocationReadService
{
    public async Task<IReadOnlyList<BudgetAllocationResponse>> GetForPeriodAsync(
        Guid periodId,
        CancellationToken cancellationToken = default)
    {
        var lines = await context.BudgetAllocationReadModels
            .AsNoTracking()
            .Where(a => a.PeriodId == periodId)
            .OrderBy(a => a.Code)
            .ToListAsync(cancellationToken);

        if (lines.Count == 0)
        {
            return [];
        }

        var codesById = await context.BudgetCodeReadModels
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var emailByUserId = await context.UserLookups
            .AsNoTracking()
            .ToDictionaryAsync(u => u.UserId, u => u.Email, cancellationToken);

        return lines.Select(line => ToResponse(line, codesById, emailByUserId)).ToList();
    }

    private static BudgetAllocationResponse ToResponse(
        BudgetAllocationReadModel line,
        IReadOnlyDictionary<Guid, BudgetCodeReadModel> codesById,
        IReadOnlyDictionary<Guid, string> emailByUserId)
    {
        var code = codesById.GetValueOrDefault(line.BudgetCodeId);

        return new BudgetAllocationResponse(
            line.Id,
            line.PeriodId,
            line.BudgetCodeId,
            line.Code,
            code?.Name ?? line.Code,
            code?.Category ?? nameof(BudgetCodeCategory.Expense),
            code?.ServiceLine,
            code?.IsActive ?? false,
            line.AmountCad,
            line.Justification,
            line.CreatedBy,
            EmailFor(line.CreatedBy, emailByUserId),
            line.ModifiedBy,
            EmailFor(line.ModifiedBy, emailByUserId),
            line.CreatedAtUtc,
            line.UpdatedAtUtc);
    }

    private static string? EmailFor(Guid? userId, IReadOnlyDictionary<Guid, string> emailByUserId) =>
        userId is { } id && emailByUserId.TryGetValue(id, out var email) ? email : null;
}
