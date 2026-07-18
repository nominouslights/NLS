using NorthernLink.Identity.Domain.Users;

namespace NorthernLink.Identity.Application.Abstractions;

/// <summary>
/// Persistence for issued refresh tokens. Not tenant-scoped (see <see cref="RefreshToken"/>'s
/// doc comment) — lookups by hash are the only access path, and a hash is unguessable, so no
/// tenant filter is needed here.
/// </summary>
public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    void Add(RefreshToken refreshToken);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
