using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Application.Inspections;

/// <summary>Maps the VehicleInspection aggregate to the module's public response contract.</summary>
public static class VehicleInspectionResponseMapper
{
    public static VehicleInspectionResponse ToResponse(VehicleInspection inspection) => new(
        inspection.Id,
        inspection.Unit,
        inspection.Type.ToString(),
        inspection.DriverName,
        inspection.Source.ToString(),
        inspection.EnteredBy,
        inspection.TripNumber,
        inspection.ManifestId,
        inspection.PerformedAt,
        inspection.OdometerKm,
        inspection.Result.ToString(),
        inspection.ChecklistItems.Select(item => new InspectionChecklistItemResponse(
            item.Group,
            item.Item,
            item.Passed)).ToList(),
        inspection.Defects.Select(defect => new InspectionDefectResponse(
            defect.Item,
            defect.Severity.ToString(),
            defect.Note)).ToList(),
        inspection.GeneratedWorkOrderId,
        inspection.CreatedAtUtc);
}
