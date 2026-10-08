using Microsoft.EntityFrameworkCore;
using Npgsql;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Vendors;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="BudgetingDbContext"/> (tenant-filtered).</summary>
internal sealed class VendorRepository(BudgetingDbContext context) : IVendorRepository
{
    /// <summary>The unique (tenant_id, normalized_name) index — see <c>VendorConfiguration</c>.</summary>
    internal const string NormalizedNameIndex = "IX_vendors_tenant_id_normalized_name";

    public Task<Vendor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Vendors.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    // Ordinal equality on an already-normalized string — a seek on the unique
    // (tenant_id, normalized_name) index rather than a case-insensitive scan.
    public Task<Vendor?> GetByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default) =>
        context.Vendors.FirstOrDefaultAsync(v => v.NormalizedName == normalizedName, cancellationToken);

    public void Add(Vendor vendor) => context.Vendors.Add(vendor);

    public void Remove(Vendor vendor) => context.Vendors.Remove(vendor);

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
            ConstraintName: NormalizedNameIndex,
        })
        {
            // Another request claimed this tenant-unique name between the handler's lookup and
            // this commit. The single failed SaveChanges persisted nothing. Unlike the Fleet /
            // Identity precedent, which detaches only ex.Entries, clear the whole tracker: the
            // audit pipeline (ModuleDbContext.AppendAuditEntries) also added snapshot and journal
            // rows (and any outbox row) for this write, and those are not in ex.Entries — left tracked, a
            // later save on this scope would commit an audit trail for a write that never happened.
            context.ChangeTracker.Clear();
            return false;
        }
    }
}
