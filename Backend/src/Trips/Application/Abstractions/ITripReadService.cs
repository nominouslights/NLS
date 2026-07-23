using NorthernLink.Trips.Application.Trips;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Read side for trip queries — returns response DTOs from <c>rm_trips</c>, skipping the
/// aggregate. Implementations are tenant-scoped (EF global query filter + Postgres RLS).
/// </summary>
public interface ITripReadService
{
    Task<IReadOnlyList<TripResponse>> GetTripsAsync(
        TripFilter filter,
        CancellationToken cancellationToken = default);

    Task<TripResponse?> GetTripAsync(Guid tripId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional narrowing for the trips list. <see cref="Date"/> is an exact service-date
/// match (DispatchBoard's "today"); <see cref="From"/>/<see cref="To"/> bound a range;
/// <see cref="OpenOnly"/> keeps only Scheduled trips with no driver ("needs coverage").
/// </summary>
public sealed record TripFilter(
    DateOnly? Date = null,
    DateOnly? From = null,
    DateOnly? To = null,
    TripStatus? Status = null,
    TripServiceType? ServiceType = null,
    Guid? ClientId = null,
    Guid? DriverId = null,
    bool OpenOnly = false);
