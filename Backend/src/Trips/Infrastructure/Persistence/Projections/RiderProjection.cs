using Microsoft.EntityFrameworkCore;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Trips.Domain.Riders;
using NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Trips.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="Rider"/> into <c>trips.rm_riders</c>. See <see cref="TripProjection"/>
/// for the worker/tenancy notes.
/// </summary>
internal sealed class RiderProjection : IProjection<TripsDbContext>
{
    public string AggregateType { get; } = AuditNames.ForAggregate(typeof(Rider));

    public async Task ApplyAsync(TripsDbContext context, Guid aggregateId, CancellationToken cancellationToken)
    {
        var rider = await context.Riders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == aggregateId, cancellationToken);

        var row = await context.RiderReadModels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == aggregateId, cancellationToken);

        if (rider is null)
        {
            if (row is not null)
            {
                context.RiderReadModels.Remove(row);
            }

            return;
        }

        if (row is null)
        {
            row = new RiderReadModel();
            Map(rider, row);
            context.RiderReadModels.Add(row);
        }
        else
        {
            Map(rider, row);
        }
    }

    public async Task RebuildAllAsync(TripsDbContext context, CancellationToken cancellationToken)
    {
        var riders = await context.Riders.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var rows = await context.RiderReadModels.IgnoreQueryFilters().ToListAsync(cancellationToken);

        var rowsById = rows.ToDictionary(row => row.Id);
        var seen = new HashSet<Guid>();

        foreach (var rider in riders)
        {
            seen.Add(rider.Id);

            if (rowsById.TryGetValue(rider.Id, out var existing))
            {
                Map(rider, existing);
            }
            else
            {
                var fresh = new RiderReadModel();
                Map(rider, fresh);
                context.RiderReadModels.Add(fresh);
            }
        }

        foreach (var (id, row) in rowsById)
        {
            if (!seen.Contains(id))
            {
                context.RiderReadModels.Remove(row);
            }
        }
    }

    private static void Map(Rider source, RiderReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.Name = source.Name;
        row.NormalizedName = source.NormalizedName;
        row.ServiceType = source.ServiceType.ToString();
        row.Contact = source.Contact;
        row.RotationDays = source.RotationDays;
        row.LastTripDate = source.LastTripDate;
        row.LastTripNumber = source.LastTripNumber;
        row.TripCount = source.TripCount;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;
    }
}
