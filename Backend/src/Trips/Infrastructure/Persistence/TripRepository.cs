using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="TripsDbContext"/> (tenant-filtered).</summary>
internal sealed class TripRepository(TripsDbContext context) : ITripRepository
{
    public void Add(Trip trip) => context.Trips.Add(trip);

    public Task<Trip?> GetByIdAsync(Guid tripId, CancellationToken cancellationToken = default) =>
        context.Trips.FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

    public Task<Trip?> GetByTripNumberAsync(string tripNumber, CancellationToken cancellationToken = default) =>
        context.Trips.FirstOrDefaultAsync(t => t.TripNumber == tripNumber, cancellationToken);

    public async Task<IReadOnlyList<Trip>> GetByRoundTripKeyAsync(
        string roundTripKey, CancellationToken cancellationToken = default) =>
        await context.Trips
            .Where(t => t.RoundTripKey == roundTripKey)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
