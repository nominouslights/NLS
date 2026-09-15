using NorthernLink.Drivers.Domain.Drivers;

namespace NorthernLink.Drivers.Application.Abstractions;

/// <summary>
/// Write-side persistence for the Driver aggregate.
/// Implementations are tenant-scoped (EF global query filter + Postgres RLS).
/// </summary>
public interface IDriverRepository
{
    Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The driver linked to an Identity user, or null. Write side on purpose: this backs the
    /// caller-owns-this-row check and the link uniqueness check, and both must see a link made
    /// moments ago. The rm_drivers copy lags by a projection poll, which for an authorization
    /// decision would mean silently denying a driver who was just onboarded.
    /// </summary>
    Task<Driver?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    void Add(Driver driver);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
