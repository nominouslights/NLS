using Microsoft.EntityFrameworkCore;
using Npgsql;
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

    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        await SystemAccess.EnableAsync(context, cancellationToken);

        return await context.Users.IgnoreQueryFilters().AnyAsync(cancellationToken);
    }

    public async Task<bool> TryAddFirstUserAsync(User user, CancellationToken cancellationToken = default)
    {
        // Opts the (now-open) connection into the app.is_system RLS escape hatch — first-run
        // setup has no tenant session yet, same tenant-less case as login/bootstrap.
        await SystemAccess.EnableAsync(context, cancellationToken);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // Serialize concurrent setup attempts and close the check-then-insert race: the lock is
        // held until this transaction commits/rolls back, so a second caller blocks here and
        // then sees the row the winner inserted. The key is an arbitrary constant unique to
        // this operation.
        await context.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(7_240_118_542_001);", cancellationToken);

        if (await context.Users.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TryAddNewUserAsync(User user, CancellationToken cancellationToken = default)
    {
        // Anonymous bootstrap flow — same tenant-less app.is_system case as the lookups above.
        await SystemAccess.EnableAsync(context, cancellationToken);

        context.Users.Add(user);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_users_email",
        })
        {
            // Someone inserted the same email between the handler's pre-check and this commit.
            // The single failed SaveChanges persisted nothing (including any other pending
            // changes on this unit of work); detach the failed entries so the context stays
            // usable and report the duplicate as a domain condition, not a 500.
            foreach (var entry in ex.Entries)
            {
                entry.State = EntityState.Detached;
            }

            return false;
        }
    }

    public void Add(User user) => context.Users.Add(user);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
