using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Codes.GetCodes;

/// <summary>Lists the tenant's whole chart of budget codes, ordered by code.</summary>
public sealed record GetBudgetCodesQuery(Guid TenantId) : IQuery<IReadOnlyList<BudgetCodeResponse>>;
