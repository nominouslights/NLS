using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.CostCentres.GetPlannedRollup;

/// <summary>
/// One period's planned expense per cost centre, plus the "No cost centre" bucket
/// (<see cref="CostCentrePlannedRollup"/>). Planned only — actuals are a later slice.
/// </summary>
public sealed record GetCostCentreRollupQuery(Guid TenantId, Guid PeriodId) : IQuery<CostCentreRollupResponse>;
