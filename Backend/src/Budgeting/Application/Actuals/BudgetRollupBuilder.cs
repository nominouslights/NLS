using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Application.Actuals;

/// <summary>
/// Folds a period's planned amounts and its actual amounts into one row per budget code — the
/// whole of the rollup's arithmetic, with nothing in it that knows where the numbers came from.
/// <para>
/// It lives in the application layer rather than inside the read service for one reason: this is
/// the part that can be wrong. A full outer join, a grouping key, a subtraction and an ordering
/// are four decisions a reader has to take on trust if they are buried in a method that also
/// opens three EF queries. Here they are a pure function over lists, so
/// "a code with spend and no plan still appears" is a unit test rather than a database fixture.
/// The read service keeps exactly the part that needs a database: fetching.
/// </para>
/// <para>
/// <b>Both sides contribute keys.</b> A code planned but not yet spent against comes out with
/// <c>ActualCad</c> zero; a code spent against but never planned comes out with
/// <c>PlannedCad</c> zero — and that second row is what a planner opens this table to find.
/// </para>
/// <para>
/// <b>Grouped by budget code id, never by the code string.</b> A code deleted and recreated under
/// the same string is a different code with a different id, and merging its plan with the old
/// code's spend would invent a comparison nobody made. The string is display and ordering only.
/// </para>
/// </summary>
public static class BudgetRollupBuilder
{
    /// <summary>
    /// One amount tagged with the code it was recorded against — a planned line or a recorded
    /// transaction, flattened to the three fields the rollup needs. <paramref name="Code"/> is
    /// the row's own snapshot of the code string, which is what lets a row whose code has since
    /// been deleted still show a label and sort into place.
    /// </summary>
    public readonly record struct CodeAmount(Guid BudgetCodeId, string Code, decimal AmountCad);

    /// <summary>
    /// What the chart currently says about a code. Resolved at read time rather than stored on
    /// either row, so re-classifying a code moves its plan and its spend together.
    /// </summary>
    public readonly record struct CodeDescriptor(string Name, string Category);

    /// <summary>
    /// The rollup, ordered by code string (ordinal — codes are normalized to upper case, and a
    /// culture-sensitive comparison would order them differently on a differently configured
    /// host). <c>DeltaCad</c> is <c>Actual − Planned</c>: positive means more moved than was
    /// planned, whichever side of the ledger the code sits on. No variance band and no percentage
    /// — the console owns those thresholds. And no tax of any kind: the platform never computes
    /// it.
    /// </summary>
    public static IReadOnlyList<BudgetRollupRowResponse> Build(
        IEnumerable<CodeAmount> planned,
        IEnumerable<CodeAmount> actual,
        IReadOnlyDictionary<Guid, CodeDescriptor> codesById)
    {
        var totals = new Dictionary<Guid, Totals>();

        foreach (var line in planned)
        {
            var current = totals.GetValueOrDefault(line.BudgetCodeId, Totals.For(line.Code));
            totals[line.BudgetCodeId] = current with { PlannedCad = current.PlannedCad + line.AmountCad };
        }

        foreach (var transaction in actual)
        {
            var current = totals.GetValueOrDefault(transaction.BudgetCodeId, Totals.For(transaction.Code));
            totals[transaction.BudgetCodeId] = current with { ActualCad = current.ActualCad + transaction.AmountCad };
        }

        return totals
            .Select(entry => ToRow(entry.Key, entry.Value, codesById))
            .OrderBy(row => row.Code, StringComparer.Ordinal)
            .ToList();
    }

    // A code the chart no longer has renders rather than disappears: its own snapshot string
    // stands in for the name, and Expense is the conservative reading of an unknown category —
    // the same fallback ActualTransactionReadService and BudgetAllocationReadService use, so the
    // rollup and the lines it is built from can never disagree about where a figure belongs.
    private static BudgetRollupRowResponse ToRow(
        Guid budgetCodeId,
        Totals totals,
        IReadOnlyDictionary<Guid, CodeDescriptor> codesById)
    {
        CodeDescriptor? code = codesById.TryGetValue(budgetCodeId, out var descriptor) ? descriptor : null;

        return new BudgetRollupRowResponse(
            budgetCodeId,
            totals.Code,
            code?.Name ?? totals.Code,
            code?.Category ?? nameof(BudgetCodeCategory.Expense),
            totals.PlannedCad,
            totals.ActualCad,
            totals.ActualCad - totals.PlannedCad);
    }

    // Code rides the accumulator rather than being looked up at the end so a row whose code the
    // chart no longer has still carries the string its plan or its spend snapshotted.
    private readonly record struct Totals(string Code, decimal PlannedCad, decimal ActualCad)
    {
        public static Totals For(string code) => new(code, 0m, 0m);
    }
}
