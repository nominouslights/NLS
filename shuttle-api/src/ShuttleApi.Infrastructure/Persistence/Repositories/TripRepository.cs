using Microsoft.EntityFrameworkCore;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Infrastructure.Persistence.Repositories;

internal sealed class TripRepository(AppDbContext dbContext) : ITripRepository
{
    public async Task<IReadOnlyList<Trip>> GetAllAsync(
        TripStatus? status = null,
        Guid? clientId = null,
        Guid? driverId = null,
        Guid? vehicleId = null,
        TripServiceType? serviceType = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Trips
            .Include(t => t.Stops)
            .Include(t => t.Passengers)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        if (clientId.HasValue)
            query = query.Where(t => t.ClientId == clientId.Value);

        if (driverId.HasValue)
            query = query.Where(t => t.DriverId == driverId.Value);

        if (vehicleId.HasValue)
            query = query.Where(t => t.VehicleId == vehicleId.Value);

        if (serviceType.HasValue)
            query = query.Where(t => t.ServiceType == serviceType.Value);

        return await query
            .OrderByDescending(t => t.ScheduledAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Trips
            .Include(t => t.Stops)
            .Include(t => t.Passengers)
            .Include(t => t.PreInspection)
                .ThenInclude(p => p!.Items)
            .Include(t => t.PostReport)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task AddAsync(Trip trip, CancellationToken cancellationToken = default)
    {
        await dbContext.Trips.AddAsync(trip, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Trip trip, CancellationToken cancellationToken = default)
    {
        dbContext.Trips.Update(trip);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Trip trip, CancellationToken cancellationToken = default)
    {
        dbContext.Trips.Remove(trip);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
