namespace ShuttleApi.Domain.Drivers;

public interface IDriverRepository
{
    Task<IReadOnlyList<Driver>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Driver?> GetDeletedByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Driver>> GetAllArchivedAsync(CancellationToken cancellationToken = default);
    Task PurgeExpiredAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
    Task<Driver?> GetByIdWithDocumentsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Driver?> GetByIdWithRosterAsync(Guid id, DateOnly rangeStart, DateOnly rangeEnd, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(Driver Driver, List<DriverRosterEntry> Entries)>> GetAllWithRosterAsync(DateOnly rangeStart, DateOnly rangeEnd, CancellationToken cancellationToken = default);
    Task<DriverDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<DriverRosterEntry?> GetRosterEntryAsync(Guid driverId, DateOnly entryDate, CancellationToken cancellationToken = default);
    Task AddAsync(Driver driver, CancellationToken cancellationToken = default);
    Task UpdateAsync(Driver driver, CancellationToken cancellationToken = default);
    Task DeleteAsync(Driver driver, CancellationToken cancellationToken = default);
}
