using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Inspections.Enter;
using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Application.Inspections.Update;

/// <summary>
/// Amends an existing inspection identified by <see cref="InspectionId"/>. Mirrors
/// <see cref="EnterInspectionCommand"/>'s editable field set, minus the identity fields:
/// <c>Type</c> and <c>TripNumber</c> are immutable on an amend (an amend corrects the contents
/// of a pre- or post-trip record, it does not change which trip or which half it is). Reuses
/// <see cref="ChecklistItemInput"/>/<see cref="DefectInput"/> from the enter path.
/// </summary>
public sealed record UpdateInspectionCommand(
    Guid InspectionId,
    InspectionSource Source,
    Guid? VehicleId,
    string Unit,
    string DriverName,
    string? EnteredBy,
    DateTimeOffset PerformedAt,
    int? OdometerKm,
    IReadOnlyList<ChecklistItemInput> Checklist,
    IReadOnlyList<DefectInput> Defects,
    IReadOnlyList<InspectionWeather> Weather,
    string? TemperatureC,
    IReadOnlyList<InspectionRoadCondition> RoadConditions,
    InspectionVisibility? Visibility,
    string? RoadAdvisories,
    InspectionFuelLevel? FuelLevel,
    IReadOnlyList<string> Issues,
    IReadOnlyList<bool> Attestations,
    string? DriverSignatureName,
    DateTimeOffset? CertifiedAt,
    bool FuelAdded,
    decimal? FuelLitres,
    decimal? FuelCostCad) : ICommand;
