using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Riders.GetRiders;

/// <summary>
/// Lists the rider directory visible to the current tenant, grouped-order
/// (ContractCrew → Community → Nihb → Charter) then name, optionally narrowed to one
/// service type and/or a case-insensitive name search.
/// </summary>
public sealed record GetRidersQuery(
    Guid TenantId,
    TripServiceType? ServiceType = null,
    string? Search = null) : IQuery<IReadOnlyList<RiderResponse>>;
