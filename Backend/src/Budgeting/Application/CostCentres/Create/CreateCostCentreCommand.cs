using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Domain.CostCentres;

namespace NorthernLink.Budgeting.Application.CostCentres.Create;

/// <summary>
/// Adds an (active) entry to the tenant's cost-centre register. Returns its id.
/// <paramref name="ActorId"/> comes from the signed token, never the body.
/// </summary>
public sealed record CreateCostCentreCommand(
    Guid TenantId,
    string? Code,
    CostCentreDetails Details,
    Guid? ActorId) : ICommand<Guid>;
