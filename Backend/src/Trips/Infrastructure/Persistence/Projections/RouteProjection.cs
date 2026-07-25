using Microsoft.EntityFrameworkCore;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Trips.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="Route"/> into <c>trips.rm_routes</c>. See
/// <see cref="TripProjection"/> for the worker/tenancy notes.
/// </summary>
internal sealed class RouteProjection : IProjection<TripsDbContext>
{
    public string AggregateType { get; } = AuditNames.ForAggregate(typeof(Route));

    public async Task ApplyAsync(TripsDbContext context, Guid aggregateId, CancellationToken cancellationToken)
    {
        var route = await context.Routes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == aggregateId, cancellationToken);

        var row = await context.RouteReadModels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == aggregateId, cancellationToken);

        if (route is null)
        {
            if (row is not null)
            {
                context.RouteReadModels.Remove(row);
            }

            return;
        }

        if (row is null)
        {
            row = new RouteReadModel();
            Map(route, row);
            context.RouteReadModels.Add(row);
        }
        else
        {
            Map(route, row);
        }
    }

    public async Task RebuildAllAsync(TripsDbContext context, CancellationToken cancellationToken)
    {
        var routes = await context.Routes.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var rows = await context.RouteReadModels.IgnoreQueryFilters().ToListAsync(cancellationToken);

        var rowsById = rows.ToDictionary(row => row.Id);
        var seen = new HashSet<Guid>();

        foreach (var route in routes)
        {
            seen.Add(route.Id);

            if (rowsById.TryGetValue(route.Id, out var existing))
            {
                Map(route, existing);
            }
            else
            {
                var fresh = new RouteReadModel();
                Map(route, fresh);
                context.RouteReadModels.Add(fresh);
            }
        }

        foreach (var (id, row) in rowsById)
        {
            if (!seen.Contains(id))
            {
                context.RouteReadModels.Remove(row);
            }
        }
    }

    private static void Map(Route source, RouteReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.Name = source.Name;
        row.Stops = [.. source.Stops];
        row.Origin = source.Origin;
        row.Destination = source.Destination;
        row.DistanceKm = source.DistanceKm;
        row.EstimatedDurationMinutes = (int)source.EstimatedDuration.TotalMinutes;
        row.RequiredLicenceClass = source.RequiredLicenceClass;
        row.Active = source.Active;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;
    }
}
