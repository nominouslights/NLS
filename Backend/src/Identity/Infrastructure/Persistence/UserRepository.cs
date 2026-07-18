using Microsoft.EntityFrameworkCore;
using NorthernLink.Identity.Application.Abstractions;
using NorthernLink.Identity.Domain.Users;

namespace NorthernLink.Identity.Infrastructure.Persistence;

/// <summary>
/// Write-side repository over <see cref="IdentityDbContext"/>. <see cref="GetByEmailAsync"/>
/// and <see cref="GetByIdAsync"/> back the anonymous login/refresh flows: they run before any
/// tenant is known, so they bypass the normal EF tenant query filter with
/// <c>IgnoreQueryFilters()</c> and additionally opt the connection into the
/// <c>app.is_system</c> RLS escape hatch via <see cref="SystemAccess"/> — otherwise Postgres
/// RLS alone would hide every row (the API-level filter is the belt, RLS is the suspenders,
/// and here we deliberately loosen both for the one legitimate tenant-less case).
/// </summary>
internal sealed class UserRepository(IdentityDbContext context) : IUserRepository
{
    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        await SystemAccess.EnableAsync(context, cancellationToken);

        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await SystemAccess.EnableAsync(context, cancellationToken);

        return await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public void Add(User user) => context.Users.Add(user);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
