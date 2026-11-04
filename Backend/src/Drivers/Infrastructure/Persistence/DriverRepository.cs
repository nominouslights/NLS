using Microsoft.EntityFrameworkCore;
using Npgsql;
using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Domain.Drivers;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="DriversDbContext"/> (tenant-filtered).</summary>
internal sealed class DriverRepository(DriversDbContext context) : IDriverRepository
{
    /// <summary>The partial unique index on (tenant_id, motive_user_id).</summary>
    internal const string MotiveUserIdIndexName = "IX_drivers_tenant_id_motive_user_id";

    public Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Drivers.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<Driver?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.Drivers.FirstOrDefaultAsync(d => d.UserId == userId, cancellationToken);

    public Task<bool> ExistsByMotiveUserIdAsync(
        string motiveUserId,
        Guid excludeDriverId,
        CancellationToken cancellationToken = default) =>
        context.Drivers.AnyAsync(
            d => d.MotiveUserId == motiveUserId && d.Id != excludeDriverId,
            cancellationToken);

    public void Add(Driver driver) => context.Drivers.Add(driver);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public async Task<bool> TrySaveMotiveUserIdAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: MotiveUserIdIndexName,
        })
        {
            // Another driver took this Motive user id between the handler's existence check and
            // this commit. The single failed SaveChanges persisted nothing; detach the failed
            // entries so the context stays usable and report the race as a domain conflict, not
            // a 500 (the Fleet PlanAssignmentRepository.TryAddAsync pattern).
            foreach (var entry in ex.Entries)
            {
                entry.State = EntityState.Detached;
            }

            return false;
        }
    }
}
