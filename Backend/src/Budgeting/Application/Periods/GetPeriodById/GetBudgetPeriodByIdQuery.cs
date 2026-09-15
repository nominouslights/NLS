using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Periods.GetPeriodById;

/// <summary>
/// One budget period with its planned totals — what the period dashboard loads, and the
/// resource the create endpoint's 201 Location header has always pointed at.
/// </summary>
public sealed record GetBudgetPeriodByIdQuery(Guid TenantId, Guid PeriodId) : IQuery<BudgetPeriodResponse>;
