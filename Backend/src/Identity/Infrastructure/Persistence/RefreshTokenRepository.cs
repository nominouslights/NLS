using Microsoft.EntityFrameworkCore;
using NorthernLink.Identity.Application.Abstractions;
using NorthernLink.Identity.Domain.Users;

namespace NorthernLink.Identity.Infrastructure.Persistence;

/// <summary>
/// Write-side repository over <see cref="IdentityDbContext"/>. No tenant filter and no
/// <see cref="SystemAccess"/> needed — refresh_tokens carries no tenant_id and has no RLS
/// policy (see <see cref="RefreshToken"/>'s doc comment); a lookup is by unguessable hash only.
/// </summary>
internal sealed class RefreshTokenRepository(IdentityDbContext context) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public void Add(RefreshToken refreshToken) => context.RefreshTokens.Add(refreshToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
