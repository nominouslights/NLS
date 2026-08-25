using Microsoft.EntityFrameworkCore;
using Npgsql;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Maintenance;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="FleetDbContext"/> (tenant-filtered).</summary>
internal sealed class MaintenancePlanRepository(FleetDbContext context) : IMaintenancePlanRepository
{
    public Task<MaintenancePlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.MaintenancePlans.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<MaintenancePlan?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.MaintenancePlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.MaintenancePlans.AnyAsync(p => p.Id == id, cancellationToken);

    public async Task<Guid?> FindIdByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        // Dumb equality — trim ownership sits with the caller, matching what the aggregate stores.
        var id = await context.MaintenancePlans
            .Where(p => p.Name == name)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return id == Guid.Empty ? null : id;
    }

    public void Add(MaintenancePlan plan) => context.MaintenancePlans.Add(plan);

    public async Task<bool> TryAddAsync(MaintenancePlan plan, CancellationToken cancellationToken = default)
    {
        context.MaintenancePlans.Add(plan);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_maintenance_plans_tenant_id_name",
        })
        {
            // Someone created a plan under this tenant-unique name between the handler's
            // lookup and this commit. The single failed SaveChanges persisted nothing;
            // detach the failed entries so the context stays usable and report the race as
            // a domain condition, not a 500 (the PlanAssignmentRepository.TryAddAsync pattern).
            foreach (var entry in ex.Entries)
            {
                entry.State = EntityState.Detached;
            }

            return false;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
