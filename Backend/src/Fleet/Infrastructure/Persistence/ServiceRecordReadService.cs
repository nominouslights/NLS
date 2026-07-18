using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Services;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>Read side — queries the read model through fleet.v_service_records and maps to the public contract.</summary>
internal sealed class ServiceRecordReadService(FleetDbContext context) : IServiceRecordReadService
{
    public async Task<IReadOnlyList<ServiceRecordResponse>> GetForVehicleAsync(
        Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var records = await context.ServiceRecordReadModels
            .AsNoTracking()
            .Where(s => s.VehicleId == vehicleId)
            .OrderByDescending(s => s.Date)
            .ToListAsync(cancellationToken);

        return records.Select(s => new ServiceRecordResponse(
            s.Id,
            s.VehicleId,
            s.Number,
            s.Date,
            s.PerformedBy,
            s.Category,
            s.OdometerKm,
            s.ItemsChanged,
            s.Reason,
            s.PartsUsed.Select(p => new ServicePartResponse(p.Sku, p.Qty)).ToList(),
            s.LaborHours,
            s.CostCad,
            s.WorkOrderId,
            s.Notes,
            s.CreatedAtUtc)).ToList();
    }
}
