using NorthernLink.Fleet.Domain.Services;

namespace NorthernLink.Fleet.Application.Services;

/// <summary>Maps the ServiceRecord aggregate to its public response contract.</summary>
public static class ServiceRecordResponseMapper
{
    public static ServiceRecordResponse ToResponse(ServiceRecord record) => new(
        record.Id,
        record.VehicleId,
        record.Number,
        record.Date,
        record.PerformedBy,
        record.Category.ToString(),
        record.OdometerKm,
        record.ItemsChanged,
        record.Reason,
        record.PartsUsed.Select(p => new ServicePartResponse(p.Sku, p.Qty)).ToList(),
        record.LaborHours,
        record.CostCad,
        record.WorkOrderId,
        record.Notes,
        record.CreatedAtUtc);
}
