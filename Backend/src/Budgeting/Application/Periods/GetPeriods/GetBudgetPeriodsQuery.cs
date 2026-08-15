using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Periods.GetPeriods;

/// <summary>Lists the tenant's budget periods, ordered by start date.</summary>
public sealed record GetBudgetPeriodsQuery(Guid TenantId) : IQuery<IReadOnlyList<BudgetPeriodResponse>>;
