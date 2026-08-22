namespace NorthernLink.Budgeting.Domain.Codes;

/// <summary>
/// Which side of the ledger a budget code governs. Architecture Section 5.3: every dollar
/// moving through Northern Link — cost or revenue — is tagged to a budget code, so a code is
/// always exactly one of the two and never both.
/// </summary>
public enum BudgetCodeCategory
{
    Revenue,
    Expense,
}
