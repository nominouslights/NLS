using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Periods.Create;

/// <summary>Creates a new (Draft) budget period. Returns the new period's id.</summary>
public sealed record CreateBudgetPeriodCommand(
    Guid TenantId,
    PeriodGranularity Granularity,
    int Year,
    int Ordinal) : ICommand<Guid>;
