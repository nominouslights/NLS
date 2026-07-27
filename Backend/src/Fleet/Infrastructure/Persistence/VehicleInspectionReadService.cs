using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Inspections;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>
/// Read side — queries the read model through fleet.v_vehicle_inspections and maps to the
/// public contract (the checklist and defects jsonb materialize with the read model).
/// </summary>
internal sealed class VehicleInspectionReadService(FleetDbContext context) : IVehicleInspectionReadService
{
    public async Task<IReadOnlyList<VehicleInspectionResponse>> GetInspectionsAsync(
        string? unit = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.VehicleInspectionReadModels.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(unit))
        {
            query = query.Where(i => i.Unit == unit.Trim());
        }

        var inspections = await query
            .OrderByDescending(i => i.PerformedAt)
            .ThenBy(i => i.Type)
            .ToListAsync(cancellationToken);

        return inspections.Select(i => new VehicleInspectionResponse(
            i.Id,
            i.VehicleId,
            i.Unit,
            i.Type,
            i.DriverName,
            i.Source,
            i.EnteredBy,
            i.TripNumber,
            i.ManifestId,
            i.PerformedAt,
            i.OdometerKm,
            i.Result,
            i.ChecklistItems.Select(item => new InspectionChecklistItemResponse(
                item.Group,
                item.Item,
                item.Passed)).ToList(),
            i.Defects.Select(defect => new InspectionDefectResponse(
                defect.Item,
                defect.Severity.ToString(),
                defect.Note)).ToList(),
            i.Weather.Select(w => w.ToString()).ToList(),
            i.TemperatureC,
            i.RoadConditions.Select(r => r.ToString()).ToList(),
            i.Visibility?.ToString(),
            i.RoadAdvisories,
            i.FuelLevel?.ToString(),
            i.Issues.ToList(),
            i.Attestations.ToList(),
            i.DriverSignatureName,
            i.CertifiedAt,
            i.FuelAdded,
            i.FuelLitres,
            i.FuelCostCad,
            i.GeneratedWorkOrderId,
            i.CreatedAtUtc)).ToList();
    }
}
