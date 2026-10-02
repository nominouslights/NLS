using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Codes;

/// <summary>
/// The chart follows the period lifecycle: a period's codes can be created, edited, retired,
/// restored, deleted, seeded or copied into only while the period
/// <see cref="BudgetPeriod.AllowsPlanChanges"/> (Draft or Open) — the same window its budget items
/// have. One place, so the seven write handlers cannot drift apart on it.
/// <para>
/// The period is looked up through the tenant-filtered repository, so another tenant's period id
/// is <see cref="BudgetPeriodErrors.NotFound"/>; a period outside Draft/Open is
/// <see cref="BudgetCodeErrors.PeriodNotEditable"/>. Reads never come through here.
/// </para>
/// </summary>
public static class BudgetCodePeriodRule
{
    public static async Task<Result> RequireEditableAsync(
        IBudgetPeriodRepository periods,
        Guid periodId,
        CancellationToken cancellationToken)
    {
        var period = await periods.GetByIdAsync(periodId, cancellationToken);
        if (period is null)
        {
            return Result.Failure(BudgetPeriodErrors.NotFound);
        }

        return period.AllowsPlanChanges
            ? Result.Success()
            : Result.Failure(BudgetCodeErrors.PeriodNotEditable);
    }
}
