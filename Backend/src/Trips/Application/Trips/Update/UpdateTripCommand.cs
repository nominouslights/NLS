using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.Update;

/// <summary>
/// Edits a trip's plan while it is still Scheduled (the aggregate rejects later edits).
/// <see cref="RouteId"/> must be the trip's CURRENT route: a catalogue trip keeps its own snapshot
/// (never re-read from the catalogue), a free-form trip (null) may edit its corridor text, and any
/// other value is refused with <c>Trips.Trip.UseChangeRoute</c> — re-routing is
/// <c>ChangeTripRouteCommand</c>'s job. On a free-form trip <see cref="OriginProvince"/>/
/// <see cref="DestinationProvince"/> and each stop's <c>Province</c> default to Manitoba when
/// absent and are refused outside the operating area; on a catalogue trip they are ignored
/// with the rest of the corridor. Trip number, template provenance, assignment, demand, status,
/// and the deadhead flag never change here — the flag moves only through ConvertToDeadhead /
/// ConvertToPassengerTrip.
/// </summary>
public sealed record UpdateTripCommand(
    Guid TripId,
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
    Guid? ClientId,
    string? ClientName,
    string? PoNumber,
    int? SeatsCapacity,
    int? SeatsMinimum,
    string? OriginProvince = null,
    string? DestinationProvince = null) : ICommand;
