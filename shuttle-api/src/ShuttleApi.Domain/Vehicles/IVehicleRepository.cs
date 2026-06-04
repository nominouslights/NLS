namespace ShuttleApi.Domain.Vehicles;

public interface IVehicleRepository
{
    Task<IReadOnlyList<Vehicle>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Vehicle?> GetDeletedByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Vehicle>> GetAllArchivedAsync(CancellationToken cancellationToken = default);
    Task PurgeExpiredAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
    Task<Vehicle?> GetByIdWithRecordsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByVinAsync(string vin, CancellationToken cancellationToken = default);
    Task<bool> ExistsByLicensePlateAsync(string licensePlate, CancellationToken cancellationToken = default);
    Task<bool> ExistsByUnitCodeAsync(string unitCode, CancellationToken cancellationToken = default);
    Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default);
    Task UpdateAsync(Vehicle vehicle, CancellationToken cancellationToken = default);
    Task DeleteAsync(Vehicle vehicle, CancellationToken cancellationToken = default);
}
