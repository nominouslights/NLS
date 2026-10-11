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

    public async Task<VendorRollupResponse> GetPlannedRollupAsync(
        Guid periodId, CancellationToken cancellationToken = default)
    {
        // The same projections the period totals read (rm_budget_allocations, rm_budget_codes),
        // classified by VendorPlannedRollup, so its total and plannedExpenseCad cannot disagree.
        var rows = await context.BudgetAllocationReadModels
            .AsNoTracking()
            .Where(a => a.PeriodId == periodId)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(a => new VendorPlannedRollup.Item(
                a.BudgetCodeId,
                a.AmountCad,
                a.VendorShares.Select(s => new VendorPlannedRollup.Share(s.VendorId, s.AmountCad)).ToList()))
            .ToList();

        var codes = await context.BudgetCodeReadModels
            .AsNoTracking()
            .Where(c => c.PeriodId == periodId)
            .Select(c => new VendorPlannedRollup.PeriodCode(c.Id, c.Category))
            .ToListAsync(cancellationToken);

        // Only the vendors the period's shares name; the rollup lists no others.
        var vendorIds = items.SelectMany(i => i.Shares).Select(s => s.VendorId).Distinct().ToList();
        List<VendorPlannedRollup.RegisterEntry> register = vendorIds.Count == 0
            ? []
            : await context.VendorReadModels
                .AsNoTracking()
                .Where(v => vendorIds.Contains(v.Id))
                .Select(v => new VendorPlannedRollup.RegisterEntry(v.Id, v.Name, v.IsActive))
                .ToListAsync(cancellationToken);

        return VendorPlannedRollup.Build(periodId, items, codes, register);
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
