using Microsoft.EntityFrameworkCore;
using ShuttleApi.Domain.Locations;

namespace ShuttleApi.Infrastructure.Persistence.Repositories;

internal sealed class SavedLocationRepository(AppDbContext dbContext) : ISavedLocationRepository
{
    public async Task<IReadOnlyList<SavedLocation>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SavedLocations
            .AsNoTracking()
            .OrderBy(l => l.Name)
            .ToListAsync(cancellationToken);

    public async Task<SavedLocation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.SavedLocations.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task<SavedLocation?> GetDeletedByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.SavedLocations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Id == id && l.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<SavedLocation>> GetAllArchivedAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SavedLocations
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(l => l.IsDeleted)
            .OrderByDescending(l => l.DeletedAt)
            .ToListAsync(cancellationToken);

    public async Task PurgeExpiredAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        var expired = await dbContext.SavedLocations
            .IgnoreQueryFilters()
            .Where(l => l.IsDeleted && l.DeletedAt < cutoffUtc)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
            return;

        dbContext.SavedLocations.RemoveRange(expired);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

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
