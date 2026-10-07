using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.Update;

/// <summary>
/// Edits a trip's plan while it is still Scheduled (the aggregate rejects later edits).
/// <see cref="RouteId"/> must be the trip's CURRENT route: a catalogue trip keeps its own snapshot
/// (never re-read from the catalogue), a free-form trip (null) may edit its corridor text, and any
/// other value is refused with <c>Trips.Trip.UseChangeRoute</c> — re-routing is
/// <c>ChangeTripRouteCommand</c>'s job. Trip number, template provenance, assignment, demand, and
/// status never change here.
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
    bool IsEmptyLeg,
    Guid? ClientId,
    string? ClientName,
    string? PoNumber,
    int? SeatsCapacity,
    int? SeatsMinimum) : ICommand;
