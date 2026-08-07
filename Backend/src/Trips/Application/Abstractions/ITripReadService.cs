using NorthernLink.Trips.Application.Trips;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Read side for trip queries — returns response DTOs from <c>rm_trips</c>, skipping the
/// aggregate. Implementations are tenant-scoped (EF global query filter + Postgres RLS).
/// </summary>
public interface ITripReadService
{
    /// <summary>
    /// The matching page of trips plus the <em>unpaged</em> total for the same filter.
    /// With no <see cref="TripFilter.Page"/>/<see cref="TripFilter.PageSize"/> the items are
    /// the complete match and the total is simply their count.
    /// </summary>
    Task<(IReadOnlyList<TripResponse> Items, int TotalCount)> GetTripsAsync(
        TripFilter filter,
        CancellationToken cancellationToken = default);

    Task<TripResponse?> GetTripAsync(Guid tripId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional narrowing for the trips list. <see cref="Date"/> is an exact service-date
/// match (DispatchBoard's "today"); <see cref="From"/>/<see cref="To"/> bound a range;
/// <see cref="OpenOnly"/> keeps only Scheduled trips with no driver ("needs coverage");
/// <see cref="AssignedOnly"/> is its counterpart (a driver is on the trip).
/// <para>
/// <see cref="Page"/>/<see cref="PageSize"/> are all-or-nothing: both set pages the result,
/// either absent returns every match. Callers that need a whole set (a driver's full history,
/// a dispatch day) simply omit them.
/// </para>
/// </summary>
public sealed record TripFilter(
    DateOnly? Date = null,
    DateOnly? From = null,
    DateOnly? To = null,
    TripStatus? Status = null,
    TripServiceType? ServiceType = null,
    Guid? ClientId = null,
    Guid? DriverId = null,
    bool OpenOnly = false,
    bool AssignedOnly = false,
    bool ExcludeCancelled = false,
    int? Page = null,
    int? PageSize = null);
