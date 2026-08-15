using Microsoft.EntityFrameworkCore;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.Integration;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>
/// Replica upserts over <see cref="BudgetingDbContext"/>. Reads go through the tenant query
/// filter (request path). The upserts run from integration handlers, whose DbContext was
/// constructed before the handler pushed the event's tenant as the ambient tenant — so the
/// context's captured TenantId is null and the query filter would match nothing; the upsert
/// therefore bypasses the filter and matches on (key, tenant) explicitly. Postgres RLS still
/// scopes the statement to the event's tenant: the session variable is read at connection open,
/// inside the ambient push.
/// </summary>
internal sealed class UserLookupRepository(BudgetingDbContext context) : IUserLookupRepository
{
    public Task<UserLookup?> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.UserLookups.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

    // Request path — the query filter is populated here, so no IgnoreQueryFilters.
    public async Task<IReadOnlyList<UserLookup>> ListAsync(CancellationToken cancellationToken = default) =>
        await context.UserLookups.AsNoTracking()
            .OrderBy(u => u.Email)
            .ToListAsync(cancellationToken);

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
            existing.Email = user.Email;
            existing.Role = user.Role;
            existing.UpdatedAtUtc = user.UpdatedAtUtc;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
