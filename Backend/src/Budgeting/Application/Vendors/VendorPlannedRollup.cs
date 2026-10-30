using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Application.Vendors;

/// <summary>
/// One period's planned expense per vendor — a pure function, so the rule is unit-testable
/// without a database (the <c>CostCentrePlannedRollup</c> precedent, whose expense classification
/// it shares with <c>PeriodPlannedTotals</c>).
/// <list type="bullet">
/// <item><b>Expense only.</b> An item counts when its code (looked up by id among the period's own
/// codes) is an Expense code — or cannot be resolved at all, which <c>PeriodPlannedTotals</c> also
/// reads as Expense. Revenue items have no shares by rule, and their payer text is not a vendor.</item>
/// <item><b>Attribution is by the item's vendor shares</b>, in dollars: a vendor's planned figure
/// is the sum of its shares; its item count is the number of expense items it has a share on (an
/// item split across three vendors counts once for each). An expense item with no shares lands in
/// <see cref="NoVendorRollup"/> with its whole amount.</item>
/// <item><b>Rows:</b> only vendors with at least one share in the period's expense items — unlike
/// the cost-centre rollup, the register is not listed in full: a vendor register grows with every
/// supplier ever paid, and a $0 row per idle supplier would bury the ones that matter. Retired
/// vendors still carrying shares are listed (flagged inactive). A share whose vendor has no read row
/// yet (projection lag) gets a row with a null name rather than vanishing from the total. Ordered
/// by name, case-insensitively, then id.</item>
/// <item><b>The total is the period's planned expense, always.</b>
/// <see cref="VendorRollupResponse.TotalPlannedExpenseCad"/> is the sum of every expense item's
/// amount, and the rows plus "No vendor" sum to it: the domain guarantees an item's shares sum to
/// its amount, and should a stored row ever disagree (written by hand), the difference is carried in
/// "No vendor"'s figure rather than silently breaking the equality.</item>
/// <item><b>Counts follow money:</b> an item that contributes any amount to a bucket is counted in
/// that bucket. So an item whose shares fall short of (or exceed) its amount is counted once for
/// each of its vendors <em>and</em> once in "No vendor", whose figure carries the difference
/// (negative when the shares exceed the amount). An item whose shares sum exactly is never counted
/// in "No vendor". Item counts therefore do not sum to the number of expense items — they never
/// did, since a split item counts once per vendor.</item>
/// <item><b>No rounding step.</b> Every amount is stored at 2 dp, and a sum of 2-dp decimals is exact.</item>
/// </list>
/// Actuals are a later slice; there is deliberately no actual field to fill with zeroes.
/// </summary>
public static class VendorPlannedRollup
{
    /// <summary>One vendor share of an item.</summary>
    public readonly record struct Share(Guid VendorId, decimal AmountCad);

    /// <summary>One budget item of the period: its code id, its amount and its vendor shares.</summary>
    public sealed record Item(Guid BudgetCodeId, decimal AmountCad, IReadOnlyList<Share> Shares);

    /// <summary>One code of the period's chart: its category as stored.</summary>
    public readonly record struct PeriodCode(Guid Id, string Category);

    /// <summary>One register entry.</summary>
    public readonly record struct RegisterEntry(Guid Id, string Name, bool IsActive);

    public static VendorRollupResponse Build(
        Guid periodId,
        IEnumerable<Item> items,
        IEnumerable<PeriodCode> codes,
        IEnumerable<RegisterEntry> register)
    {
        var categoryByCode = codes.ToDictionary(c => c.Id, c => c.Category);
        var vendorsById = register.ToDictionary(v => v.Id);

        var planned = new Dictionary<Guid, decimal>();
        var itemCounts = new Dictionary<Guid, int>();
        decimal total = 0m;
        decimal noVendorPlanned = 0m;
        var noVendorItems = 0;

        foreach (var item in items)
        {
            if (categoryByCode.TryGetValue(item.BudgetCodeId, out var category)
                && category == nameof(BudgetCodeCategory.Revenue))
            {
                continue;
            }

            total += item.AmountCad;

            if (item.Shares.Count == 0)
            {
                noVendorPlanned += item.AmountCad;
                noVendorItems++;
                continue;
            }

            foreach (var share in item.Shares)
            {
                planned[share.VendorId] = planned.GetValueOrDefault(share.VendorId) + share.AmountCad;
                itemCounts[share.VendorId] = itemCounts.GetValueOrDefault(share.VendorId) + 1;
            }

            // Zero whenever the domain's sum rule held when the item was written. When it is not,
            // the item contributes to "No vendor" and is counted there too — see the class comment.
            var unattributed = item.AmountCad - item.Shares.Sum(share => share.AmountCad);
            if (unattributed != 0m)
            {
                noVendorPlanned += unattributed;
                noVendorItems++;
            }
        }

        var rows = planned
            .Select(entry =>
            {
                var vendor = vendorsById.TryGetValue(entry.Key, out var found) ? found : (RegisterEntry?)null;
                return new VendorRollupRow(
                    entry.Key,
                    vendor?.Name,
                    vendor?.IsActive ?? false,
                    itemCounts[entry.Key],
                    entry.Value);
            })
            .OrderBy(row => row.Name is null ? 1 : 0)
            .ThenBy(row => row.Name?.ToUpperInvariant(), StringComparer.Ordinal)
            .ThenBy(row => row.VendorId)
            .ToList();

        return new VendorRollupResponse(
            periodId,
            rows,
            new NoVendorRollup(noVendorItems, noVendorPlanned),
            total);
    }
}

/// <summary>
/// Body of GET /api/budgeting/periods/{id}/rollups/vendors. <see cref="TotalPlannedExpenseCad"/>
/// equals the sum of every row's <c>PlannedCad</c> plus <see cref="NoVendor"/>'s, and equals the
/// period's own <c>plannedExpenseCad</c>.
/// </summary>
public sealed record VendorRollupResponse(
    Guid PeriodId,
    IReadOnlyList<VendorRollupRow> Vendors,
    NoVendorRollup NoVendor,
    decimal TotalPlannedExpenseCad);

/// <summary>
/// One vendor's planned expense in the period: the sum of its shares, over
/// <see cref="ItemCount"/> expense items. <see cref="Name"/> is null (and <see cref="IsActive"/>
/// false) only while the vendor's read row is still a projection poll behind.
/// </summary>
public sealed record VendorRollupRow(
    Guid VendorId,
    string? Name,
    bool IsActive,
    int ItemCount,
    decimal PlannedCad);

/// <summary>
/// Expense planned on items with no vendor shares, plus any part of an item its shares do not
/// cover; <see cref="ItemCount"/> counts every item contributing to <see cref="PlannedCad"/>.
/// </summary>
public sealed record NoVendorRollup(int ItemCount, decimal PlannedCad);
