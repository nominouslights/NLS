namespace NorthernLink.Budgeting.Domain.Allocations;

/// <summary>
/// Whether a budget item is day-to-day running cost or an asset purchase. Classification only —
/// it drives no calculation (no depreciation, no tax; the platform never computes tax). Stored and
/// sent as its name, so the member spellings are part of the API contract.
/// </summary>
public enum BudgetSpendType
{
    Operating,
    Capital,
}
