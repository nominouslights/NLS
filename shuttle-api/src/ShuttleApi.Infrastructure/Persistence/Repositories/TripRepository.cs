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
            .AsNoTracking()
            .Include(t => t.Stops)
            .Include(t => t.Passengers)
            .Include(t => t.CargoItems)
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
                .ThenInclude(p => p.EmailLogs)
            .Include(t => t.CargoItems)
            .Include(t => t.PreInspection)
                .ThenInclude(p => p!.Items)
            .Include(t => t.PostReport)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<Trip?> GetDeletedByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Trips
            .IgnoreQueryFilters()
            .Include(t => t.Stops)
            .Include(t => t.Passengers)
            .FirstOrDefaultAsync(t => t.Id == id && t.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<Trip>> GetAllArchivedAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Trips
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(t => t.Stops)
            .Include(t => t.Passengers)
            .Where(t => t.IsDeleted)
            .OrderByDescending(t => t.DeletedAt)
            .ToListAsync(cancellationToken);

    public async Task PurgeExpiredAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        var expired = await dbContext.Trips
            .IgnoreQueryFilters()
            .Where(t => t.IsDeleted && t.DeletedAt < cutoffUtc)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
            return;

        dbContext.Trips.RemoveRange(expired);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Trip>> GetByDateRangeAsync(
        DateOnly from, DateOnly to, TripServiceType serviceType,
        CancellationToken cancellationToken = default) =>
        await dbContext.Trips
            .AsNoTracking()
            .Include(t => t.Stops)
            .Include(t => t.Passengers)
            .Where(t => t.ServiceType == serviceType
                && DateOnly.FromDateTime(t.ScheduledAt) >= from
                && DateOnly.FromDateTime(t.ScheduledAt) <= to)
            .OrderBy(t => t.ScheduledAt)
            .ToListAsync(cancellationToken);

    public async Task<Trip?> FindCommunityTripAsync(
        DateOnly date, string direction, string destination, CancellationToken cancellationToken = default)
    {
        var destinationStopName = destination.Equals("LeafRapids", StringComparison.OrdinalIgnoreCase)
            ? "Leaf Rapids"
            : "Lynn Lake";

        return await dbContext.Trips
            .Include(t => t.Stops)
            .Include(t => t.Passengers)
            .Where(t => t.ServiceType == TripServiceType.Community
                && DateOnly.FromDateTime(t.ScheduledAt) == date
                && t.Stops.Any(s => s.LocationName == destinationStopName)
                && (t.Passengers.Any(p => p.Direction == direction) || !t.Passengers.Any()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> BookingReferenceExistsAsync(
        string reference, CancellationToken cancellationToken = default) =>
        await dbContext.TripPassengers
            .AnyAsync(p => p.BookingReference == reference, cancellationToken);

    public async Task<TripPassenger?> GetPassengerByReferenceAsync(
        string reference, CancellationToken cancellationToken = default) =>
        await dbContext.TripPassengers
            .FirstOrDefaultAsync(p => p.BookingReference == reference, cancellationToken);

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

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
