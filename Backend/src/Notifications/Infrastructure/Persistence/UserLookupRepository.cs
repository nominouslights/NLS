using Microsoft.EntityFrameworkCore;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Application.Integration;

namespace NorthernLink.Notifications.Infrastructure.Persistence;

/// <summary>
/// Replica reads and upserts over <see cref="NotificationsDbContext"/>. Both members run from
/// integration handlers — never from a request — so neither leans on the tenant query filter:
/// each bypasses it and matches the tenant explicitly, the way Budgeting's
/// <c>UserLookupRepository.UpsertAsync</c> does. (The polling consumer resolves the handler
/// scope inside the event's ambient-tenant push, so the filter would in fact be populated; the
/// explicit predicate is what keeps that true by construction rather than by call order.)
/// Postgres RLS still scopes every statement to the event's tenant through the session variable
/// read at connection open, inside the push.
/// </summary>
internal sealed class UserLookupRepository(NotificationsDbContext context) : IUserLookupRepository
{
    public async Task<IReadOnlyList<UserLookup>> ListActiveByRolesAsync(
        Guid tenantId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        if (roles.Count == 0)
        {
            return [];
        }

        var wanted = roles.ToArray();
        return await context.UserLookups
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive && wanted.Contains(u.Role))
            .OrderBy(u => u.Email)
            .ToListAsync(cancellationToken);
    }

    public async Task UpsertAsync(UserLookup user, CancellationToken cancellationToken = default)
    {
        var existing = await context.UserLookups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                u => u.UserId == user.UserId && u.TenantId == user.TenantId,
                cancellationToken);

        if (existing is null)
        {
            context.UserLookups.Add(user);
        }
        else
        {
            // Every column the event carries is reassigned, nulls included: the event is a full
            // snapshot, so an omission here would not leave a field stale — it would make it
            // permanently unchangeable, silently.
            existing.Email = user.Email;
            existing.FullName = user.FullName;
            existing.Role = user.Role;
            existing.IsActive = user.IsActive;
            existing.UpdatedAtUtc = user.UpdatedAtUtc;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
