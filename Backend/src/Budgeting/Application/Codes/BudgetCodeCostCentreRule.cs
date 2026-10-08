using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.CostCentres;

namespace NorthernLink.Budgeting.Application.Codes;

/// <summary>
/// A budget code's cost centre must name an entry of the tenant's cost-centre register — shared
/// by create and edit for the <see cref="BudgetCodeParentRule"/> reason. It lives here rather
/// than on the aggregate because it needs the register, which an aggregate cannot query.
/// <list type="bullet">
/// <item>Blank → nothing to check (normalizes to null on the aggregate).</item>
/// <item>No entry with that (trimmed, ordinal) code → <see cref="BudgetCodeErrors.CostCentreNotFound"/> (400).</item>
/// <item>A retired entry → <see cref="BudgetCodeErrors.CostCentreRetired"/> (409) — <b>only on
/// change</b>. An update that leaves the code's existing value as it is passes without a lookup,
/// so retiring a cost centre never freezes the codes already carrying it.</item>
/// </list>
/// <para>
/// <b>Values the aggregate itself will refuse are left to it</b> — a cost centre on a Revenue code
/// (<see cref="BudgetCodeErrors.CostCentreNotAllowedForRevenue"/>) or one over
/// <see cref="BudgetCode.CostCentreMaxLength"/> characters. Checking the register first would
/// report "not in the register" for what is really a category or length mistake.
/// </para>
/// </summary>
public static class BudgetCodeCostCentreRule
{
    /// <param name="currentCostCentre">
    /// The code's value before this write: null on create; the stored value on update.
    /// </param>
    public static async Task<Result> ValidateAsync(
        ICostCentreRepository costCentres,
        BudgetCodeDetails details,
        string? currentCostCentre,
        CancellationToken cancellationToken)
    {
        var requested = CostCentre.NormalizeCode(details.CostCentre);
        if (requested.Length == 0
            || requested.Length > BudgetCode.CostCentreMaxLength
            || details.Category == BudgetCodeCategory.Revenue)
        {
            return Result.Success();
        }

        if (currentCostCentre is not null && string.Equals(requested, currentCostCentre, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        var entry = await costCentres.GetByCodeAsync(requested, cancellationToken);
        if (entry is null)
        {
            return Result.Failure(BudgetCodeErrors.CostCentreNotFound);
        }

        return entry.IsActive
            ? Result.Success()
            : Result.Failure(BudgetCodeErrors.CostCentreRetired);
    }
}
