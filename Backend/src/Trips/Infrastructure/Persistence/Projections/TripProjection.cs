using Microsoft.EntityFrameworkCore;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Trips.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="Trip"/> into <c>trips.rm_trips</c>. Same shape as
/// <see cref="TripManifestProjection"/>: every query bypasses the tenant filter (the
/// worker runs on a pinned <c>app.is_system</c> session with no ambient tenant), and
/// nothing is saved here — the worker commits with the checkpoint in one save.
/// </summary>
internal sealed class TripProjection : IProjection<TripsDbContext>
{
    public string AggregateType { get; } = AuditNames.ForAggregate(typeof(Trip));

    public async Task ApplyAsync(TripsDbContext context, Guid aggregateId, CancellationToken cancellationToken)
    {
        var trip = await context.Trips
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == aggregateId, cancellationToken);

        var row = await context.TripReadModels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == aggregateId, cancellationToken);

        if (trip is null)
        {
            if (row is not null)
            {
                context.TripReadModels.Remove(row);
            }

            return;
        }

        if (row is null)
        {
            row = new TripReadModel();
            Map(trip, row);
            context.TripReadModels.Add(row);
        }
        else
        {
            Map(trip, row);
        }
    }

    public async Task RebuildAllAsync(TripsDbContext context, CancellationToken cancellationToken)
    {
        var trips = await context.Trips.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var rows = await context.TripReadModels.IgnoreQueryFilters().ToListAsync(cancellationToken);

        var rowsById = rows.ToDictionary(row => row.Id);
        var seen = new HashSet<Guid>();

        foreach (var trip in trips)
        {
            seen.Add(trip.Id);

            if (rowsById.TryGetValue(trip.Id, out var existing))
            {
                Map(trip, existing);
            }
            else
            {
                var fresh = new TripReadModel();
                Map(trip, fresh);
                context.TripReadModels.Add(fresh);
            }
        }

        foreach (var (id, row) in rowsById)
        {
            if (!seen.Contains(id))
            {
                context.TripReadModels.Remove(row);
            }
        }
    }

    private static void Map(Trip source, TripReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;

        row.TripNumber = source.TripNumber;
        row.ServiceDate = source.ServiceDate;
        row.WindowStart = source.WindowStart;
        row.WindowEnd = source.WindowEnd;
        row.ServiceType = source.ServiceType.ToString();

        row.RouteId = source.RouteId;
        row.RouteName = source.RouteName;
        row.Origin = source.Origin;
        row.Destination = source.Destination;
        row.Stops = [.. source.Stops];
        row.DistanceKm = source.DistanceKm;

        row.ScheduleTemplateId = source.ScheduleTemplateId;
        row.RoundTripKey = source.RoundTripKey;
        row.Direction = source.Direction?.ToString();
        row.IsEmptyLeg = source.IsEmptyLeg;

        row.ClientId = source.ClientId;
        row.ClientName = source.ClientName;
        row.PoNumber = source.PoNumber;

        row.DriverId = source.DriverId;
        row.DriverName = source.DriverName;
        row.VehicleUnit = source.VehicleUnit;

        row.SeatsCapacity = source.SeatsCapacity;
        row.SeatsConfirmed = source.SeatsConfirmed;
        row.SeatsMinimum = source.SeatsMinimum;
        row.DemandGuaranteed = source.DemandGuaranteed;

        row.Status = source.Status.ToString();
        row.ManifestId = source.ManifestId;
        row.CompletedAtUtc = source.CompletedAtUtc;
        row.CancelledReason = source.CancelledReason;

        row.CreatedAtUtc = source.CreatedAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;
    }
}
