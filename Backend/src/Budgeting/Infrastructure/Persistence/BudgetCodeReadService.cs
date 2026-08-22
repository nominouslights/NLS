using Microsoft.EntityFrameworkCore;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.Codes;
using NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>
/// Read side — queries budgeting.rm_budget_codes and maps to the public contract, resolving the
/// three id columns (parent, owner, created/modified by) to display values.
/// <para>
/// <b>Two queries and two dictionaries, not a join.</b> Three nullable Guid columns each needing
/// a GroupJoin/DefaultIfEmpty produces SQL nobody wants to read or debug, for a chart of a few
/// dozen rows that is already being materialized in full. The parent lookup needs no query at
/// all — the parent is another row in the set already in hand.
/// </para>
/// <para>
/// <b>And not denormalized onto the read model either.</b> The projection base only ever reads
/// its own source aggregate, so an email copied into <c>rm_budget_codes</c> would never be
/// refreshed when the user changes — it would go stale silently the day Identity grows a rename.
/// Resolving here means the display value is always current as of the read.
/// </para>
/// </summary>
internal sealed class BudgetCodeReadService(BudgetingDbContext context) : IBudgetCodeReadService
{
    public async Task<IReadOnlyList<BudgetCodeResponse>> GetCodesAsync(
        CancellationToken cancellationToken = default)
    {
        var codes = await context.BudgetCodeReadModels
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .ToListAsync(cancellationToken);

        if (codes.Count == 0)
        {
            return [];
        }

        var emailByUserId = await context.UserLookups
            .AsNoTracking()
            .ToDictionaryAsync(u => u.UserId, u => u.Email, cancellationToken);

        var parentsById = codes.ToDictionary(c => c.Id, c => (c.Code, c.Name));

        return codes.Select(code => ToResponse(code, parentsById, emailByUserId)).ToList();
    }

    private static BudgetCodeResponse ToResponse(
        BudgetCodeReadModel code,
        IReadOnlyDictionary<Guid, (string Code, string Name)> parentsById,
        IReadOnlyDictionary<Guid, string> emailByUserId)
    {
        // A parent id that resolves to nothing renders as null rather than throwing: the delete
        // handler refuses to orphan children, but a row written before that guard existed — or by
        // hand — should still list.
        (string Code, string Name)? parent =
            code.ParentCodeId is { } parentId && parentsById.TryGetValue(parentId, out var found)
                ? found
                : null;

        return new BudgetCodeResponse(
            code.Id,
            code.Code,
            code.Name,
            code.Description,
            code.Category,
            code.ServiceLine,
            code.CostCentre,
            code.ParentCodeId,
            parent?.Code,
            parent?.Name,
            code.GlAccountCode,
            code.TaxTreatment,
            code.BudgetOwnerUserId,
            EmailFor(code.BudgetOwnerUserId, emailByUserId),
            code.ReviewFrequency,
            code.IsActive,
            code.CreatedBy,
            EmailFor(code.CreatedBy, emailByUserId),
            code.ModifiedBy,
            EmailFor(code.ModifiedBy, emailByUserId),
            code.CreatedAtUtc,
            code.UpdatedAtUtc);
    }

    private static string? EmailFor(Guid? userId, IReadOnlyDictionary<Guid, string> emailByUserId) =>
        userId is { } id && emailByUserId.TryGetValue(id, out var email) ? email : null;
}
