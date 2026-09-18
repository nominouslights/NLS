using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Actuals.GetRollup;

/// <summary>
/// One period's plan against its spend, one row per budget code, ordered by code. NotFound when
/// the period does not exist. Codes planned but not spent against and codes spent against but
/// never planned both appear — the second is the row zero-based budgeting exists to surface.
/// </summary>
public sealed record GetBudgetRollupQuery(Guid TenantId, Guid PeriodId)
    : IQuery<IReadOnlyList<BudgetRollupRowResponse>>;
