using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Application.Trips;
using NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// Read side — queries <c>trips.rm_trips</c> (tenant query filter + RLS) and maps to
/// the public contract. Status/service-type filters compare against the stored string
/// forms; openOnly keeps Scheduled trips with no driver ("needs coverage").
/// </summary>
internal sealed class TripReadService(TripsDbContext context) : ITripReadService
{
    public async Task<(IReadOnlyList<TripResponse> Items, int TotalCount)> GetTripsAsync(
        TripFilter filter,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TripReadModel> query = context.TripReadModels.AsNoTracking();

        if (filter.Date is { } date)
        {
            query = query.Where(t => t.ServiceDate == date);
        }

        if (filter.From is { } from)
        {
            query = query.Where(t => t.ServiceDate >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(t => t.ServiceDate <= to);
        }

        if (filter.Status is { } status)
        {
            var statusName = status.ToString();
            query = query.Where(t => t.Status == statusName);
        }

        if (filter.ServiceType is { } serviceType)
        {
            var serviceTypeName = serviceType.ToString();
            query = query.Where(t => t.ServiceType == serviceTypeName);
        }

        if (filter.ClientId is { } clientId)
        {
            query = query.Where(t => t.ClientId == clientId);
        }

        if (filter.DriverId is { } driverId)
        {
            query = query.Where(t => t.DriverId == driverId);
        }

        if (filter.OpenOnly)
        {
            var scheduled = Domain.Trips.TripStatus.Scheduled.ToString();
            query = query.Where(t => t.Status == scheduled && t.DriverId == null);
        }

        if (filter.AssignedOnly)
        {
            query = query.Where(t => t.DriverId != null);
        }

        if (filter.ExcludeCancelled)
        {
            var cancelled = Domain.Trips.TripStatus.Cancelled.ToString();
            query = query.Where(t => t.Status != cancelled);
        }

        // Counted before paging — the total describes the filtered set, not the page.
        var totalCount = await query.CountAsync(cancellationToken);

        // Deterministic ordering is a paging correctness requirement, not a preference:
        // TripNumber is the unique tiebreaker that keeps consecutive pages from overlapping.
        query = query
            .OrderBy(t => t.ServiceDate)
            .ThenBy(t => t.WindowStart)
            .ThenBy(t => t.TripNumber);

        if (filter is { Page: { } page, PageSize: { } pageSize })
        {
            query = query.Skip((page - 1) * pageSize).Take(pageSize);
        }

        var trips = await query.ToListAsync(cancellationToken);
        var billing = await BillingForAsync(trips.Select(t => t.Id).ToList(), cancellationToken);

        return (trips.Select(t => ToResponse(t, billing.GetValueOrDefault(t.Id))).ToList(), totalCount);
    }

    public async Task<TripResponse?> GetTripAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        var trip = await context.TripReadModels
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        if (trip is null)
        {
            return null;
        }

        var billing = await BillingForAsync([tripId], cancellationToken);
        return ToResponse(trip, billing.GetValueOrDefault(tripId));
    }

    /// <summary>
    /// Billing state for the page's trips, fetched separately rather than joined: the replica
    /// is maintained by an integration handler on its own schedule, and keeping it out of the
    /// main query leaves the paging query's ordering and OFFSET plan untouched. One extra
    /// round trip over at most a page of ids.
    /// </summary>
    private async Task<Dictionary<Guid, TripBilling>> BillingForAsync(
        IReadOnlyList<Guid> tripIds,
        CancellationToken cancellationToken)
    {
        if (tripIds.Count == 0)
        {
            return [];
        }

        return await context.TripBillings
            .AsNoTracking()
            .Where(b => tripIds.Contains(b.TripId))
            .ToDictionaryAsync(b => b.TripId, cancellationToken);
    }

    private static TripResponse ToResponse(TripReadModel t, TripBilling? billing) => new(
        t.Id,
        t.TripNumber,
        t.ServiceDate,
        t.WindowStart,
        t.WindowEnd,
        t.ServiceType,
        t.RouteId,
        t.RouteName,
        t.Origin,
        t.Destination,
        t.Stops.Select(stop => new TripStopResponse(
            stop.Name,
            stop.Order,
            stop.StopId,
            stop.Latitude,
            stop.Longitude,
            stop.OutboundOffsetMinutes,
            stop.ReturnOffsetMinutes)).ToList(),
        t.DistanceKm,
        t.ScheduleTemplateId,
        t.RoundTripKey,
        t.Direction,
        t.IsEmptyLeg,
        t.ClientId,
        t.ClientName,
        t.PoNumber,
        t.DriverId,
        t.DriverName,
        t.VehicleId,
        t.VehicleUnit,
        t.SeatsCapacity,
        t.SeatsConfirmed,
        t.SeatsMinimum,
        t.DemandGuaranteed,
        t.Status,
        t.ManifestId,
        t.HasPostTripInspection,
        t.OperationsFinishedAtUtc,
        t.CompletedAtUtc,
        t.CancelledReason,
        t.WrittenOffReason,
        t.CreatedAtUtc,
        t.UpdatedAtUtc,
        billing is null
            ? null
            : new TripBillingResponse(
                billing.State,
                billing.InvoiceId,
                billing.InvoiceNumber,
                billing.QboInvoiceId,
                billing.QboEnteredDate,
                billing.PaymentConfirmedDate));
}
