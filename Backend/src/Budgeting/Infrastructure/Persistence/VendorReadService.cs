using Microsoft.EntityFrameworkCore;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.Vendors;
using NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>
/// Read side — queries budgeting.rm_vendors (tenant query filter + RLS) and maps to the public
/// contract. Like every rm_* read, it is a projection poll behind the write side, so a vendor
/// created a moment ago can briefly be absent from the list or 404 by id.
/// </summary>
internal sealed class VendorReadService(BudgetingDbContext context) : IVendorReadService
{
    public async Task<IReadOnlyList<VendorResponse>> GetVendorsAsync(
        bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = context.VendorReadModels.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(v => v.IsActive);
        }

        var rows = await query
            .OrderBy(v => v.NormalizedName)
            .ToListAsync(cancellationToken);

        return rows.Select(ToResponse).ToList();
    }

    public async Task<VendorResponse?> GetVendorAsync(Guid vendorId, CancellationToken cancellationToken = default)
    {
        var row = await context.VendorReadModels
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vendorId, cancellationToken);

        return row is null ? null : ToResponse(row);
    }

    private static VendorResponse ToResponse(VendorReadModel row) => new(
        row.Id,
        row.Name,
        row.ContactName,
        row.Email,
        row.Phone,
        row.Address,
        row.Notes,
        row.GstRegistrationNumber,
        row.QboDisplayName,
        row.DefaultBudgetCode,
        row.IsActive,
        row.CreatedBy,
        row.ModifiedBy,
        row.CreatedAtUtc,
        row.UpdatedAtUtc);
}
