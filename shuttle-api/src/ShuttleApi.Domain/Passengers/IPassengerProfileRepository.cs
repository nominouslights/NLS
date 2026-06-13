namespace ShuttleApi.Domain.Passengers;

public interface IPassengerProfileRepository
{
    Task<IReadOnlyList<PassengerProfile>> SearchAsync(
        Guid clientId, string query, CancellationToken cancellationToken = default);

    Task<PassengerProfile?> FindByNormalizedNameAsync(
        Guid clientId, string normalizedName, CancellationToken cancellationToken = default);

    Task AddAsync(PassengerProfile profile, CancellationToken cancellationToken = default);

    Task UpdateAsync(PassengerProfile profile, CancellationToken cancellationToken = default);

    Task PurgeExpiredAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
}
