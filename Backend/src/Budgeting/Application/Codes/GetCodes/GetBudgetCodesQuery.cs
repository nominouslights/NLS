using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Codes.GetCodes;

/// <summary>Lists one period's whole chart of budget codes (retired included), ordered by code.</summary>
public sealed record GetBudgetCodesQuery(Guid TenantId, Guid PeriodId) : IQuery<IReadOnlyList<BudgetCodeResponse>>;
