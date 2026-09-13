using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Application.Inspections.Enter;

/// <summary>
/// Enters a pre- or post-trip inspection from the trip workflow (or, later, the Driver App).
/// The pre-trip section fields (weather/road/visibility/fuel) apply to a
/// <see cref="InspectionType.PreTrip"/>; the log/certification/fuel-added fields to a
/// <see cref="InspectionType.PostTrip"/>. Returns the new inspection's id.
/// </summary>
public sealed record EnterInspectionCommand(
    Guid TenantId,
    InspectionSource Source,
    InspectionType Type,
    string? TripNumber,
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
    decimal? FuelCostCad) : ICommand<Guid>;

/// <summary>One checklist row on an inspection request.</summary>
public sealed record ChecklistItemInput(string? Group, string Item, bool Passed);

/// <summary>
/// One defect on an inspection request. Resolution fields are deliberately absent: the wire can
/// report a fault, never clear one — that stays with the resolve endpoint and work-order
/// completion. <paramref name="RecurrenceOfInspectionId"/> is the one pointer a caller may set,
/// for the Re-report path where a dispatcher knows a cleared fault is back and does not want to
/// wait for the next DVIR.
/// </summary>
public sealed record DefectInput(
    string Item,
    InspectionDefectSeverity Severity,
    string? Note,
    Guid? RecurrenceOfInspectionId = null);
