namespace ShuttleApi.Domain.Trips;

public interface ITripRepository
{
    Task<IReadOnlyList<Trip>> GetAllAsync(
        TripStatus? status = null,
        Guid? clientId = null,
        Guid? driverId = null,
        Guid? vehicleId = null,
        TripServiceType? serviceType = null,
        CancellationToken cancellationToken = default);

    Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Trip?> GetDeletedByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Trip>> GetAllArchivedAsync(CancellationToken cancellationToken = default);

    Task PurgeExpiredAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Trip>> GetByDateRangeAsync(
        DateOnly from, DateOnly to, TripServiceType serviceType,
        CancellationToken cancellationToken = default);

    Task<Trip?> FindCommunityTripAsync(
        DateOnly date, string direction, string destination, CancellationToken cancellationToken = default);

    Task<bool> BookingReferenceExistsAsync(
        string reference, CancellationToken cancellationToken = default);

    Task<TripPassenger?> GetPassengerByReferenceAsync(
        string reference, CancellationToken cancellationToken = default);

    Task AddAsync(Trip trip, CancellationToken cancellationToken = default);

    Task UpdateAsync(Trip trip, CancellationToken cancellationToken = default);

    Task DeleteAsync(Trip trip, CancellationToken cancellationToken = default);
}
