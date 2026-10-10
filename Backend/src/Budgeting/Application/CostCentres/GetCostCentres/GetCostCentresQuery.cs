using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.CostCentres.GetCostCentres;

/// <summary>
/// Lists the tenant's cost-centre register, ordered by code. Active entries only unless
/// <paramref name="IncludeInactive"/> — the budget-code picker wants the active ones; the
/// register screen asks for everything.
/// </summary>
public sealed record GetCostCentresQuery(Guid TenantId, bool IncludeInactive)
    : IQuery<IReadOnlyList<CostCentreResponse>>;
