using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Application.Periods;

/// <summary>
/// How a period's planned revenue and planned expense are built from its budget items — a pure
/// function, so the rule is unit-testable without a database and the list and single-period reads
/// cannot disagree on it.
/// <para>
/// <b>Each item's category comes from its own period's code.</b> Codes belong to a period, so the
/// lookup is keyed on (period, code id): an item is classified by the code it was planned against
/// in <em>its</em> period, never by a same-string code of another period that a planner may have
/// re-classified since. An item whose code cannot be resolved in its period counts as Expense —
/// the same conservative reading the item list gives it.
/// </para>
/// </summary>
public static class PeriodPlannedTotals
{
    /// <summary>One budget item's contribution: its period, its code id and its amount.</summary>
    public readonly record struct ItemAmount(Guid PeriodId, Guid BudgetCodeId, decimal AmountCad);

    /// <summary>One code's classification, as stored (<c>Revenue</c> / <c>Expense</c>).</summary>
    public readonly record struct CodeCategory(Guid PeriodId, Guid CodeId, string Category);

    public sealed record Totals(decimal RevenueCad, decimal ExpenseCad)
    {
        public static readonly Totals Zero = new(0m, 0m);
    }

    /// <summary>Planned (revenue, expense) per period id. A period with no items is absent.</summary>
    public static IReadOnlyDictionary<Guid, Totals> Sum(
        IEnumerable<ItemAmount> items,
        IEnumerable<CodeCategory> codes)
    {
        var categoryByCode = new Dictionary<(Guid PeriodId, Guid CodeId), string>();
        foreach (var code in codes)
        {
            categoryByCode[(code.PeriodId, code.CodeId)] = code.Category;
        }

        var totals = new Dictionary<Guid, Totals>();
        foreach (var item in items)
        {
            var isRevenue = categoryByCode.TryGetValue((item.PeriodId, item.BudgetCodeId), out var category)
                && category == nameof(BudgetCodeCategory.Revenue);

            var current = totals.GetValueOrDefault(item.PeriodId, Totals.Zero);
            totals[item.PeriodId] = isRevenue
                ? current with { RevenueCad = current.RevenueCad + item.AmountCad }
                : current with { ExpenseCad = current.ExpenseCad + item.AmountCad };
        }

        return totals;
    }
}
