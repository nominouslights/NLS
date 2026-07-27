using Microsoft.EntityFrameworkCore;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Trips.Domain.Stops;
using NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Trips.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="Stop"/> into <c>trips.rm_stops</c>. See <see cref="TripProjection"/>
/// for the worker/tenancy notes.
/// </summary>
internal sealed class StopProjection : IProjection<TripsDbContext>
{
    public string AggregateType { get; } = AuditNames.ForAggregate(typeof(Stop));

    public async Task ApplyAsync(TripsDbContext context, Guid aggregateId, CancellationToken cancellationToken)
    {
        var stop = await context.Stops
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == aggregateId, cancellationToken);

        var row = await context.StopReadModels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == aggregateId, cancellationToken);

        if (stop is null)
        {
            if (row is not null)
            {
                context.StopReadModels.Remove(row);
            }

            return;
        }

        if (row is null)
        {
            row = new StopReadModel();
            Map(stop, row);
            context.StopReadModels.Add(row);
        }
        else
        {
            Map(stop, row);
        }
    }

    public async Task RebuildAllAsync(TripsDbContext context, CancellationToken cancellationToken)
    {
        var stops = await context.Stops.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var rows = await context.StopReadModels.IgnoreQueryFilters().ToListAsync(cancellationToken);

        var rowsById = rows.ToDictionary(row => row.Id);
        var seen = new HashSet<Guid>();

        foreach (var stop in stops)
        {
            seen.Add(stop.Id);

            if (rowsById.TryGetValue(stop.Id, out var existing))
            {
                Map(stop, existing);
            }
            else
            {
                var fresh = new StopReadModel();
                Map(stop, fresh);
                context.StopReadModels.Add(fresh);
            }
        }

        foreach (var (id, row) in rowsById)
        {
            if (!seen.Contains(id))
            {
                context.StopReadModels.Remove(row);
            }
        }
    }

    private static void Map(Stop source, StopReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.Name = source.Name;
        row.Type = source.Type?.ToString();
        row.Street = source.Address.Street;
        row.City = source.Address.City;
        row.Province = source.Address.Province;
        row.PostalCode = source.Address.PostalCode;
        row.Country = source.Address.Country;
        row.Latitude = source.Coordinate.Latitude;
        row.Longitude = source.Coordinate.Longitude;
        row.Notes = source.Notes;
        row.Active = source.Active;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;
    }
}
