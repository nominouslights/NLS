using NorthernLink.Drivers.Domain.Drivers;

namespace NorthernLink.Drivers.Application.Abstractions;

/// <summary>
/// Write-side persistence for the Driver aggregate.
/// Implementations are tenant-scoped (EF global query filter + Postgres RLS).
/// </summary>
public interface IDriverRepository
{
    Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(Driver driver);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
