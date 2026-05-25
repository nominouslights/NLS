using Microsoft.EntityFrameworkCore;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Infrastructure.Persistence.Repositories;

internal sealed class DriverRepository(AppDbContext dbContext) : IDriverRepository
{
    public async Task<IReadOnlyList<Driver>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Drivers
            .Include(d => d.Documents)
            .OrderBy(d => d.LastName)
            .ThenBy(d => d.FirstName)
            .ToListAsync(cancellationToken);

    public async Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Drivers.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<Driver?> GetByIdWithDocumentsAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Drivers
            .Include(d => d.Documents)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<Driver?> GetByIdWithRosterAsync(
        Guid id,
        DateOnly rangeStart,
        DateOnly rangeEnd,
        CancellationToken cancellationToken = default) =>
        await dbContext.Drivers
            .Include(d => d.RosterEntries.Where(r => r.EntryDate >= rangeStart && r.EntryDate <= rangeEnd))
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<(Driver Driver, List<DriverRosterEntry> Entries)>> GetAllWithRosterAsync(
        DateOnly rangeStart,
        DateOnly rangeEnd,
        CancellationToken cancellationToken = default)
    {
        var drivers = await dbContext.Drivers
            .Include(d => d.RosterEntries.Where(r => r.EntryDate >= rangeStart && r.EntryDate <= rangeEnd))
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
