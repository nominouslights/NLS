using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Stops.GetStops;

/// <summary>All of the tenant's stops (active and inactive — the screen filters client-side).</summary>
public sealed record GetStopsQuery(Guid TenantId) : IQuery<IReadOnlyList<StopResponse>>;
