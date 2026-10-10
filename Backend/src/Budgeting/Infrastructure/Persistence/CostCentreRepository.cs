using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.CostCentres;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="BudgetingDbContext"/> (tenant-filtered).</summary>
internal sealed class CostCentreRepository(BudgetingDbContext context) : ICostCentreRepository
{
    /// <summary>The unique (tenant_id, code) index — see <c>CostCentreConfiguration</c>.</summary>
    internal const string CodeIndex = "IX_cost_centres_tenant_id_code";

    public Task<CostCentre?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.CostCentres.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    // Ordinal equality on the already-trimmed code — a seek on the unique (tenant_id, code) index.
    public Task<CostCentre?> GetByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default) =>
        context.CostCentres.FirstOrDefaultAsync(c => c.Code == normalizedCode, cancellationToken);

    public Task<bool> HasChildrenAsync(Guid parentId, CancellationToken cancellationToken = default) =>
        context.CostCentres.AnyAsync(c => c.ParentId == parentId, cancellationToken);

    public Task<bool> HasActiveChildrenAsync(Guid parentId, CancellationToken cancellationToken = default) =>
        context.CostCentres.AnyAsync(c => c.ParentId == parentId && c.IsActive, cancellationToken);

    public void Add(CostCentre costCentre) => context.CostCentres.Add(costCentre);

    public void Remove(CostCentre costCentre) => context.CostCentres.Remove(costCentre);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: CodeIndex,
        })
        {
            // Another request created this tenant-unique code between the handler's lookup and
            // this commit; the failed SaveChanges persisted nothing. Clear the whole tracker, not
            // just ex.Entries — the VendorRepository reason: the audit pipeline also added
            // snapshot/journal (and outbox) rows for this write, and a later save on this scope
            // must not commit an audit trail for a write that never happened.
            context.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<ICostCentreRegisterLock> LockRegisterAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        // Own the transaction only when the caller has not already opened one; a nested caller's
        // commit is then a no-op and the outer transaction decides.
        var transaction = context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            // Transaction-scoped advisory lock (the UserRepository.TryAddFirstUserAsync and
            // ModuleMigrationRunner primitive), released by Postgres at commit or rollback, so a
            // crashed request can never leak it. The key is a 64-bit hash of a fixed namespace plus
            // the tenant id: no table, no migration. One key per tenant on purpose — see the
            // interface comment; the register is low-volume, so serializing its writes is cheap.
            // A hash collision only makes two unrelated tenants (or an unrelated advisory-lock
            // user) wait for each other briefly; it can never let a race through.
            var key = $"budgeting.cost-centre-register:{tenantId:D}";
            await context.Database.ExecuteSqlAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }

            throw;
        }

        return new RegisterLock(transaction);
    }

    private sealed class RegisterLock(IDbContextTransaction? transaction) : ICostCentreRegisterLock
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

        // Disposing an uncommitted EF transaction rolls it back, releasing the lock.
        public ValueTask DisposeAsync() => transaction?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
