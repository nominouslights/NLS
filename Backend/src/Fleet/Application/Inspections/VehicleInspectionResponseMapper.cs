using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Application.Inspections;

/// <summary>Maps the VehicleInspection aggregate to the module's public response contract.</summary>
public static class VehicleInspectionResponseMapper
{
    public static VehicleInspectionResponse ToResponse(VehicleInspection inspection) => new(
        inspection.Id,
        inspection.VehicleId,
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
            item.Passed,
            // EffectiveState, not State: a pre-NL-PTI-01 row has no State, and resolving that
            // fallback here rather than in four client apps is the whole point of the property.
            item.EffectiveState.ToString(),
            item.Note)).ToList(),
        inspection.Defects.Select(defect => new InspectionDefectResponse(
            defect.Item,
            defect.Severity.ToString(),
            defect.Note)).ToList(),
        inspection.Weather.Select(w => w.ToString()).ToList(),
        inspection.TemperatureC,
        inspection.RoadConditions.Select(r => r.ToString()).ToList(),
        inspection.Visibility?.ToString(),
        inspection.RoadAdvisories,
        inspection.FuelLevel?.ToString(),
        inspection.Issues.ToList(),
        inspection.Attestations.ToList(),
        inspection.DriverSignatureName,
        inspection.CertifiedAt,
        inspection.FuelAdded,
        inspection.FuelLitres,
        inspection.FuelCostCad,
        inspection.GeneratedWorkOrderId,
        inspection.CreatedAtUtc,
        inspection.CarrierAcknowledgedBy,
        inspection.CarrierAcknowledgedAtUtc,
        inspection.CarrierAcknowledgementNote,
        inspection.CertificationStatement);
}
