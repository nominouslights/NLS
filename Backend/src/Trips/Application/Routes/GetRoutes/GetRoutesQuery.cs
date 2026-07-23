using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Routes.GetRoutes;

/// <summary>All of the tenant's routes (active and inactive — the panel filters client-side).</summary>
public sealed record GetRoutesQuery(Guid TenantId) : IQuery<IReadOnlyList<RouteResponse>>;
