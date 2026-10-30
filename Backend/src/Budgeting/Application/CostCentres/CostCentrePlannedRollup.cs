using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Application.CostCentres;

/// <summary>
/// One period's planned expense per cost centre — a pure function, so the rule is unit-testable
/// without a database (the <c>PeriodPlannedTotals</c> precedent, whose classification it shares).
/// <list type="bullet">
/// <item><b>Expense only.</b> An item counts when its code (looked up by id among the period's
/// own codes) is an Expense code — or cannot be resolved at all, which
/// <c>PeriodPlannedTotals</c> also reads as Expense. That keeps the invariant the tests pin:
/// the rows plus the "No cost centre" bucket sum to the period's <c>plannedExpenseCad</c>.</item>
/// <item><b>Attribution is by the code's cost-centre string</b> matched ordinally to the
/// register's code. A code with no cost centre, or an item whose code is unresolvable, lands in
/// <see cref="NoCostCentreRollup"/>.</item>
/// <item><b>Rows:</b> every active register entry (a base with nothing planned shows $0 — under
/// zero-based budgeting that is information, not noise), plus any retired entry this period's
/// expense codes still carry. A string no register entry matches (impossible after the
/// <c>AddCostCentres</c> backfill, but rows written by hand exist) gets a row of its own with a
/// null id and its string as the name, rather than vanishing from the total.</item>
/// <item><b>No rounding step.</b> Item amounts are stored at 2 dp (<c>BudgetAllocation.Round</c>),
/// and a sum of 2-dp decimals is exact.</item>
/// </list>
/// Actuals are a later slice; there is deliberately no actual field to fill with zeroes.
/// </summary>
public static class CostCentrePlannedRollup
{
    /// <summary>One budget item of the period: its code id and its amount.</summary>
    public readonly record struct ItemAmount(Guid BudgetCodeId, decimal AmountCad);

    /// <summary>One code of the period's chart: its category as stored, and its cost centre.</summary>
    public readonly record struct PeriodCode(Guid Id, string Category, string? CostCentre);

    /// <summary>One register entry.</summary>
    public readonly record struct RegisterEntry(
        Guid Id, string Code, string Name, bool IsActive, Guid? ParentId, Guid? OwnerUserId);

    /// <summary>How a user id renders: the name when set, and the email always.</summary>
    public readonly record struct UserDisplay(string Email, string? FullName);

    public static CostCentreRollupResponse Build(
        Guid periodId,
        IEnumerable<ItemAmount> items,
        IEnumerable<PeriodCode> codes,
        IEnumerable<RegisterEntry> register,
        IReadOnlyDictionary<Guid, UserDisplay> users)
    {
        var codesById = codes.ToDictionary(c => c.Id);
        var entries = register.ToList();
        var entriesByCode = entries.ToDictionary(e => e.Code, StringComparer.Ordinal);
        var entriesById = entries.ToDictionary(e => e.Id);

        var planned = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var itemCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var codeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        decimal noCostCentrePlanned = 0m;
        var noCostCentreItems = 0;
        var noCostCentreCodes = 0;

        foreach (var code in codesById.Values)
        {
            if (code.Category == nameof(BudgetCodeCategory.Revenue))
            {
                continue;
            }

            if (code.CostCentre is { Length: > 0 } costCentre)
            {
                codeCounts[costCentre] = codeCounts.GetValueOrDefault(costCentre) + 1;
                planned.TryAdd(costCentre, 0m);
                itemCounts.TryAdd(costCentre, 0);
            }
            else
            {
                noCostCentreCodes++;
            }
        }

        foreach (var item in items)
        {
            if (!codesById.TryGetValue(item.BudgetCodeId, out var code))
            {
                // Unresolvable code: Expense, as PeriodPlannedTotals reads it, with no cost centre.
                noCostCentrePlanned += item.AmountCad;
                noCostCentreItems++;
                continue;
            }

            if (code.Category == nameof(BudgetCodeCategory.Revenue))
            {
                continue;
            }

            if (code.CostCentre is { Length: > 0 } costCentre)
            {
                planned[costCentre] += item.AmountCad;
                itemCounts[costCentre]++;
            }
            else
            {
                noCostCentrePlanned += item.AmountCad;
                noCostCentreItems++;
            }
        }

        var rows = new List<CostCentreRollupRow>();

        foreach (var entry in entries.Where(e => e.IsActive || planned.ContainsKey(e.Code)))
        {
            Guid? parentId = entry.ParentId;
            var parentCode = parentId is { } pid && entriesById.TryGetValue(pid, out var parent) ? parent.Code : null;
            var owner = entry.OwnerUserId is { } ownerId && users.TryGetValue(ownerId, out var display)
                ? display
                : (UserDisplay?)null;

            rows.Add(new CostCentreRollupRow(
                entry.Id,
                entry.Code,
                entry.Name,
                entry.IsActive,
                entry.ParentId,
                parentCode,
                entry.OwnerUserId,
                owner?.FullName,
                owner?.Email,
                codeCounts.GetValueOrDefault(entry.Code),
                itemCounts.GetValueOrDefault(entry.Code),
                planned.GetValueOrDefault(entry.Code)));
        }

        foreach (var unregistered in planned.Keys.Where(code => !entriesByCode.ContainsKey(code)))
        {
            rows.Add(new CostCentreRollupRow(
                null,
                unregistered,
                unregistered,
                false,
                null,
                null,
                null,
                null,
                null,
                codeCounts.GetValueOrDefault(unregistered),
                itemCounts.GetValueOrDefault(unregistered),
                planned[unregistered]));
        }

        rows.Sort((a, b) => string.CompareOrdinal(a.Code, b.Code));

        var total = rows.Sum(r => r.PlannedCad) + noCostCentrePlanned;

        return new CostCentreRollupResponse(
            periodId,
            rows,
            new NoCostCentreRollup(noCostCentreCodes, noCostCentreItems, noCostCentrePlanned),
            total);
    }
}

/// <summary>
/// Body of GET /api/budgeting/periods/{id}/rollups/cost-centres. <see cref="TotalPlannedExpenseCad"/>
/// equals the sum of every row's <c>PlannedCad</c> plus <see cref="NoCostCentre"/>'s, and equals
/// the period's own <c>plannedExpenseCad</c>.
/// </summary>
public sealed record CostCentreRollupResponse(
    Guid PeriodId,
    IReadOnlyList<CostCentreRollupRow> CostCentres,
    NoCostCentreRollup NoCostCentre,
    decimal TotalPlannedExpenseCad);

/// <summary>
/// One cost centre's planned expense in the period. <see cref="CostCentreId"/> is null only for a
/// string carried by a code that matches no register entry.
/// </summary>
public sealed record CostCentreRollupRow(
    Guid? CostCentreId,
    string Code,
    string Name,
    bool IsActive,
    Guid? ParentId,
    string? ParentCode,
    Guid? OwnerUserId,
    string? OwnerName,
    string? OwnerEmail,
    int BudgetCodeCount,
    int ItemCount,
    decimal PlannedCad);

/// <summary>Expense planned on codes with no cost centre (or on a code that cannot be resolved).</summary>
public sealed record NoCostCentreRollup(int BudgetCodeCount, int ItemCount, decimal PlannedCad);
