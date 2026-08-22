using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.Create;

/// <summary>
/// Creates an ad-hoc trip (CreateTripWizard). The trip number is issued by the
/// per-tenant sequence, never supplied. When <see cref="RouteId"/> is set the route's
/// name/stops/origin/destination/distance are snapshotted from the route and the
/// free-form fields are ignored; otherwise the free-form fields describe the corridor.
/// A trip is never created unassigned: <see cref="DriverId"/> and <see cref="VehicleId"/>
/// are required (nullable only so the handler can refuse with a domain error instead of a
/// binding failure). The driver is validated against driver_lookup and the name
/// snapshotted from it; the vehicle must be a real fleet vehicle in vehicle_lookup, whose
/// unit number and seating capacity are snapshotted — there is no free-form vehicle path.
/// </summary>
public sealed record CreateTripCommand(
    Guid TenantId,
    DateOnly ServiceDate,
    TimeOnly WindowStart,
    TimeOnly? WindowEnd,
    TripServiceType ServiceType,
    Guid? RouteId,
    string? RouteName,
    string? Origin,
    string? Destination,
    IReadOnlyList<RouteStop> Stops,
    int DistanceKm,
    TripDirection? Direction,
    bool IsEmptyLeg,
    Guid? ClientId,
    string? ClientName,
    string? PoNumber,
    Guid? DriverId,
    Guid? VehicleId,
    int? SeatsMinimum) : ICommand<Guid>;
