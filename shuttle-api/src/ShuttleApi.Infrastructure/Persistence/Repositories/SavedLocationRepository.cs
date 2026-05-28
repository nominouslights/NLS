using Microsoft.EntityFrameworkCore;
using ShuttleApi.Domain.Locations;

namespace ShuttleApi.Infrastructure.Persistence.Repositories;

internal sealed class SavedLocationRepository(AppDbContext dbContext) : ISavedLocationRepository
{
    public async Task<IReadOnlyList<SavedLocation>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SavedLocations.OrderBy(l => l.Name).ToListAsync(cancellationToken);

    public async Task<SavedLocation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.SavedLocations.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task AddAsync(SavedLocation location, CancellationToken cancellationToken = default)
    {
        await dbContext.SavedLocations.AddAsync(location, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(SavedLocation location, CancellationToken cancellationToken = default)
    {
        dbContext.SavedLocations.Update(location);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(SavedLocation location, CancellationToken cancellationToken = default)
    {
        dbContext.SavedLocations.Remove(location);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
