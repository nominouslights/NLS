namespace ShuttleApi.Domain.Trips;

public interface ITripRepository
{
    Task<IReadOnlyList<Trip>> GetAllAsync(
        TripStatus? status = null,
        Guid? clientId = null,
        Guid? driverId = null,
        CancellationToken cancellationToken = default);

    Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Trip trip, CancellationToken cancellationToken = default);

    Task UpdateAsync(Trip trip, CancellationToken cancellationToken = default);

    Task DeleteAsync(Trip trip, CancellationToken cancellationToken = default);
}
