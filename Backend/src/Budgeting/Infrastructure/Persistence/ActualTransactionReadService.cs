using Microsoft.EntityFrameworkCore;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.Actuals;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>
/// Read side — queries budgeting.rm_actual_transactions for one inclusive date range and maps to
/// the public contract, resolving each row's code to its current name, category, service line and
/// active flag, and each actor id to an email.
/// <para>
/// Three queries and two dictionaries, not a join — the <see cref="BudgetAllocationReadService"/>
/// reasoning, unchanged: the period's rows, the tenant's chart and the tenant's users are all
/// small sets already being materialized in full, and dictionary lookups read better than the SQL
/// a three-way GroupJoin/DefaultIfEmpty would produce. Resolving here rather than denormalizing
/// onto the read row is what keeps a re-classified code's figures in the right column.
/// </para>
/// <para>
/// Ordered by date then code — the order a ledger is read in, and the order the console renders
/// without re-sorting. The tie-break on code is the row's own snapshot string, not the code's
/// current name, so the order cannot shuffle when someone renames a code.
/// </para>
/// <para>
/// A row whose code is missing renders rather than throws: <c>Name = Code</c>,
/// <c>Category = Expense</c>, <c>IsCodeActive = false</c>. The usage probe refuses to delete a
/// referenced code, so this is a row written by hand or before that guard — it should still list,
/// and listing it as a retired expense is the conservative reading.
/// </para>
/// </summary>
internal sealed class ActualTransactionReadService(BudgetingDbContext context) : IActualTransactionReadService
{
    public async Task<IReadOnlyList<ActualTransactionResponse>> GetForDateRangeAsync(
        DateOnly startsOn,
        DateOnly endsOn,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.ActualTransactionReadModels
            .AsNoTracking()
            .Where(t => t.OccurredOn >= startsOn && t.OccurredOn <= endsOn)
            .OrderBy(t => t.OccurredOn)
            .ThenBy(t => t.Code)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return [];
        }

        var codesById = await context.BudgetCodeReadModels
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var emailByUserId = await context.UserLookups
            .AsNoTracking()
            .ToDictionaryAsync(u => u.UserId, u => u.Email, cancellationToken);

        return rows.Select(row => ToResponse(row, codesById, emailByUserId)).ToList();
    }

    private static ActualTransactionResponse ToResponse(
        ActualTransactionReadModel row,
        IReadOnlyDictionary<Guid, BudgetCodeReadModel> codesById,
        IReadOnlyDictionary<Guid, string> emailByUserId)
    {
        var code = codesById.GetValueOrDefault(row.BudgetCodeId);

        return new ActualTransactionResponse(
            row.Id,
            row.BudgetCodeId,
            row.Code,
            code?.Name ?? row.Code,
            code?.Category ?? nameof(BudgetCodeCategory.Expense),
            code?.ServiceLine,
            code?.IsActive ?? false,
            row.AmountCad,
            row.OccurredOn,
            row.Description,
            row.Source,
            row.ExternalReference,
            row.CreatedBy,
            EmailFor(row.CreatedBy, emailByUserId),
            row.ModifiedBy,
            EmailFor(row.ModifiedBy, emailByUserId),
            row.CreatedAtUtc,
            row.UpdatedAtUtc);
    }

    private static string? EmailFor(Guid? userId, IReadOnlyDictionary<Guid, string> emailByUserId) =>
        userId is { } id && emailByUserId.TryGetValue(id, out var email) ? email : null;
}
