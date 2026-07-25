using NorthernLink.Identity.Domain.Users;

namespace NorthernLink.Identity.Application.Abstractions;

/// <summary>
/// Persistence for admin bootstrap tokens. <see cref="GetActiveAsync"/> backs the anonymous
/// bootstrap endpoint (no tenant known from a token yet), so implementations must bypass the
/// normal tenant-scoped query filter for it, same caveat as <see cref="IUserRepository"/>.
/// </summary>
public interface IAdminBootstrapTokenRepository
{
    /// <summary>The latest unconsumed token for <paramref name="tenantId"/>, or null if none.</summary>
    Task<AdminBootstrapToken?> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken = default);

    void Add(AdminBootstrapToken token);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
