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
/// <para>
/// <b>Callers lock first.</b> A register lookup that passes can still be followed by a
/// cost-centre delete before the budget code commits. Callers therefore take
/// <see cref="LockIfCheckedAsync"/> before <see cref="ValidateAsync"/> and commit it after their
/// save. The delete handler probes usage under the same lock.
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
        if (RegisterCheckFor(details, currentCostCentre) is not { } requested)
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

    /// <summary>
    /// Takes the register's (tenant, code) lock (<see cref="ICostCentreRepository.LockCodeAsync"/>)
    /// when, and only when, <see cref="ValidateAsync"/> would look the code up. The caller holds
    /// the returned scope across <see cref="ValidateAsync"/> and its save, then commits it. That
    /// stops a concurrent cost-centre delete from passing its usage probe between this register
    /// lookup and the commit that starts carrying the string. Returns null when there is nothing
    /// to lock: a blank or unchanged value, or one the aggregate will refuse anyway. An unchanged
    /// value is already committed on this code, so the delete's probe sees it without a lock.
    /// </summary>
    public static async Task<ICostCentreCodeLock?> LockIfCheckedAsync(
        ICostCentreRepository costCentres,
        Guid tenantId,
        BudgetCodeDetails details,
        string? currentCostCentre,
        CancellationToken cancellationToken) =>
        RegisterCheckFor(details, currentCostCentre) is { } requested
            ? await costCentres.LockCodeAsync(tenantId, requested, cancellationToken)
            : null;

    /// <summary>The normalized code <see cref="ValidateAsync"/> must look up, or null when it skips the register.</summary>
    private static string? RegisterCheckFor(BudgetCodeDetails details, string? currentCostCentre)
    {
        var requested = CostCentre.NormalizeCode(details.CostCentre);
        if (requested.Length == 0
            || requested.Length > BudgetCode.CostCentreMaxLength
            || details.Category == BudgetCodeCategory.Revenue)
        {
            return null;
        }

        return currentCostCentre is not null && string.Equals(requested, currentCostCentre, StringComparison.Ordinal)
            ? null
            : requested;
    }
}
