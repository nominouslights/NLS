using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.CostCentres.GetCostCentreById;

/// <summary>One register entry, retired or not — the POST 201 <c>Location</c> target.</summary>
public sealed record GetCostCentreByIdQuery(Guid TenantId, Guid CostCentreId) : IQuery<CostCentreResponse>;
