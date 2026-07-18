using Microsoft.EntityFrameworkCore;
using NorthernLink.Identity.Application.Abstractions;
using NorthernLink.Identity.Domain.Users;

namespace NorthernLink.Identity.Infrastructure.Persistence;

/// <summary>
/// Write-side repository over <see cref="IdentityDbContext"/>. <see cref="GetActiveAsync"/>
/// backs the anonymous bootstrap endpoint — same tenant-less caveat as
/// <see cref="UserRepository"/>: bypasses the EF query filter and opts into
/// <see cref="SystemAccess"/> so RLS doesn't hide the row either.
/// </summary>
internal sealed class AdminBootstrapTokenRepository(IdentityDbContext context) : IAdminBootstrapTokenRepository
{
    public async Task<AdminBootstrapToken?> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await SystemAccess.EnableAsync(context, cancellationToken);

        return await context.AdminBootstrapTokens
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId && t.ConsumedAtUtc == null)
            .OrderByDescending(t => t.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(AdminBootstrapToken token) => context.AdminBootstrapTokens.Add(token);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
