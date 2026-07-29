using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
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
    public async Task<IReadOnlyList<TripResponse>> GetTripsAsync(
        TripFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = context.TripReadModels.AsNoTracking();

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

        var trips = await query
            .OrderBy(t => t.ServiceDate)
            .ThenBy(t => t.WindowStart)
            .ThenBy(t => t.TripNumber)
            .ToListAsync(cancellationToken);

        return trips.Select(ToResponse).ToList();
    }

    public async Task<TripResponse?> GetTripAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        var trip = await context.TripReadModels
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        return trip is null ? null : ToResponse(trip);
    }

    private static TripResponse ToResponse(TripReadModel t) => new(
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
        t.Stops.Select(stop => new TripStopResponse(stop.Name, stop.Order)).ToList(),
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
        t.CompletedAtUtc,
        t.CancelledReason,
        t.CreatedAtUtc,
        t.UpdatedAtUtc);
}
