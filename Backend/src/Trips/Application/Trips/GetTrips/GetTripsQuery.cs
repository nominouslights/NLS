using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;

namespace NorthernLink.Trips.Application.Trips.GetTrips;

/// <summary>Trips list — DispatchBoard (date = today), the Trips screen, driver history.</summary>
public sealed record GetTripsQuery(Guid TenantId, TripFilter Filter)
    : IQuery<IReadOnlyList<TripResponse>>;
