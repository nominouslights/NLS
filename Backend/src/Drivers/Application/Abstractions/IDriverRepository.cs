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

    /// <summary>
    /// True when a driver other than <paramref name="excludeDriverId"/> in the current tenant
    /// already carries this Motive user id. An existence query (nothing loaded or tracked), write
    /// side so a mapping made moments ago is visible.
    /// </summary>
    Task<bool> ExistsByMotiveUserIdAsync(string motiveUserId, Guid excludeDriverId, CancellationToken cancellationToken = default);

    void Add(Driver driver);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves pending changes, returning false (and persisting nothing) when the per-tenant Motive
    /// user id unique index refuses the write — i.e. another driver took the id between the
    /// handler's <see cref="ExistsByMotiveUserIdAsync"/> check and this commit. Only that
    /// constraint is translated; any other failure still throws.
    /// </summary>
    Task<bool> TrySaveMotiveUserIdAsync(CancellationToken cancellationToken = default);
}
