using Microsoft.EntityFrameworkCore;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Infrastructure.Persistence.Repositories;

internal sealed class VehicleRepository(AppDbContext dbContext) : IVehicleRepository
{
    public async Task<IReadOnlyList<Vehicle>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Vehicles
            .Include(v => v.ServiceRecords)
            .Include(v => v.InspectionRecords)
            .Where(v => !v.IsDeleted)
            .OrderBy(v => v.UnitCode)
            .ToListAsync(cancellationToken);

    public async Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Vehicles
            .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, cancellationToken);

    public async Task<Vehicle?> GetDeletedByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Vehicles
            .Include(v => v.ServiceRecords)
            .Include(v => v.InspectionRecords)
            .FirstOrDefaultAsync(v => v.Id == id && v.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<Vehicle>> GetAllArchivedAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Vehicles
            .Include(v => v.ServiceRecords)
            .Include(v => v.InspectionRecords)
            .Where(v => v.IsDeleted)
            .OrderByDescending(v => v.DeletedAt)
            .ToListAsync(cancellationToken);

    public async Task PurgeExpiredAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        var expired = await dbContext.Vehicles
            .Where(v => v.IsDeleted && v.DeletedAt < cutoffUtc)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
            return;

        dbContext.Vehicles.RemoveRange(expired);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Vehicle?> GetByIdWithRecordsAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Vehicles
            .Include(v => v.ServiceRecords)
            .Include(v => v.InspectionRecords)
            .Include(v => v.FuelEntries)
            .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, cancellationToken);

    public async Task<bool> ExistsByVinAsync(string vin, CancellationToken cancellationToken = default) =>
        await dbContext.Vehicles
            .AnyAsync(v => v.VIN == vin && !v.IsDeleted, cancellationToken);

    public async Task<bool> ExistsByLicensePlateAsync(string licensePlate, CancellationToken cancellationToken = default) =>
        await dbContext.Vehicles
            .AnyAsync(v => v.LicensePlate == licensePlate && !v.IsDeleted, cancellationToken);

    public async Task<bool> ExistsByUnitCodeAsync(string unitCode, CancellationToken cancellationToken = default) =>
        await dbContext.Vehicles
            .AnyAsync(v => v.UnitCode == unitCode && !v.IsDeleted, cancellationToken);

    public async Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        await dbContext.Vehicles.AddAsync(vehicle, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        dbContext.Vehicles.Update(vehicle);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        dbContext.Vehicles.Remove(vehicle);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
