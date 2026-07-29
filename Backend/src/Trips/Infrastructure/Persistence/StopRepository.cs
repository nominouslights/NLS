using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Stops;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="TripsDbContext"/> (tenant-filtered).</summary>
internal sealed class StopRepository(TripsDbContext context) : IStopRepository
{
    public void Add(Stop stop) => context.Stops.Add(stop);

    public Task<Stop?> GetByIdAsync(Guid stopId, CancellationToken cancellationToken = default) =>
        context.Stops.FirstOrDefaultAsync(s => s.Id == stopId, cancellationToken);

    public async Task<IReadOnlyList<Stop>> GetByIdsAsync(
        IReadOnlyCollection<Guid> stopIds, CancellationToken cancellationToken = default)
    {
        if (stopIds.Count == 0)
        {
            return [];
        }

        var ids = stopIds.ToHashSet();
        return await context.Stops
            .Where(s => ids.Contains(s.Id))
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
