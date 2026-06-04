using Microsoft.EntityFrameworkCore;
using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Infrastructure.Persistence.Repositories;

internal sealed class DriverRepository(AppDbContext dbContext, IFileStorageService fileStorageService) : IDriverRepository
{
    public async Task<IReadOnlyList<Driver>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Drivers
            .Include(d => d.Documents)
            .Where(d => !d.IsDeleted)
            .OrderBy(d => d.LastName)
            .ThenBy(d => d.FirstName)
            .ToListAsync(cancellationToken);

    public async Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Drivers.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, cancellationToken);

    public async Task<Driver?> GetDeletedByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Drivers
            .Include(d => d.Documents)
            .FirstOrDefaultAsync(d => d.Id == id && d.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<Driver>> GetAllArchivedAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Drivers
            .Include(d => d.Documents)
            .Where(d => d.IsDeleted)
            .OrderByDescending(d => d.DeletedAt)
            .ToListAsync(cancellationToken);

    public async Task PurgeExpiredAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        var expired = await dbContext.Drivers
            .Include(d => d.Documents)
            .Where(d => d.IsDeleted && d.DeletedAt < cutoffUtc)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
            return;

        foreach (var driver in expired)
        {
            foreach (var doc in driver.Documents)
                await fileStorageService.DeleteAsync(doc.StorageKey, cancellationToken);
        }

        dbContext.Drivers.RemoveRange(expired);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Driver?> GetByIdWithDocumentsAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Drivers
            .Include(d => d.Documents)
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, cancellationToken);

    public async Task<Driver?> GetByIdWithRosterAsync(
        Guid id,
        DateOnly rangeStart,
        DateOnly rangeEnd,
        CancellationToken cancellationToken = default) =>
        await dbContext.Drivers
            .Include(d => d.RosterEntries.Where(r => r.EntryDate >= rangeStart && r.EntryDate <= rangeEnd))
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<(Driver Driver, List<DriverRosterEntry> Entries)>> GetAllWithRosterAsync(
        DateOnly rangeStart,
        DateOnly rangeEnd,
        CancellationToken cancellationToken = default)
    {
        var drivers = await dbContext.Drivers
            .Include(d => d.RosterEntries.Where(r => r.EntryDate >= rangeStart && r.EntryDate <= rangeEnd))
            .Where(d => !d.IsDeleted)
            .OrderBy(d => d.LastName)
            .ThenBy(d => d.FirstName)
            .ToListAsync(cancellationToken);

        return drivers
            .Select(d => (d, d.RosterEntries.ToList()))
            .ToList();
    }

    public async Task<DriverDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        await dbContext.DriverDocuments.FirstOrDefaultAsync(doc => doc.Id == documentId, cancellationToken);

    public async Task<DriverRosterEntry?> GetRosterEntryAsync(Guid driverId, DateOnly entryDate, CancellationToken cancellationToken = default) =>
        await dbContext.DriverRosterEntries.FirstOrDefaultAsync(
            r => r.DriverId == driverId && r.EntryDate == entryDate, cancellationToken);

    public async Task AddAsync(Driver driver, CancellationToken cancellationToken = default)
    {
        await dbContext.Drivers.AddAsync(driver, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Driver driver, CancellationToken cancellationToken = default)
    {
        dbContext.Drivers.Update(driver);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Driver driver, CancellationToken cancellationToken = default)
    {
        dbContext.Drivers.Remove(driver);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
