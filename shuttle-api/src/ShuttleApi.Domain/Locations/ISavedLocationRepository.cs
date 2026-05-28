namespace ShuttleApi.Domain.Locations;

public interface ISavedLocationRepository
{
    Task<IReadOnlyList<SavedLocation>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SavedLocation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(SavedLocation location, CancellationToken cancellationToken = default);
    Task UpdateAsync(SavedLocation location, CancellationToken cancellationToken = default);
    Task DeleteAsync(SavedLocation location, CancellationToken cancellationToken = default);
}
