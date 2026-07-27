namespace NorthernLink.Fleet.Application.Inspections;

/// <summary>
/// The Fleet module's public representation of a vehicle inspection — the shape every
/// frontend consumes. Enum-typed fields travel as their enum names: Type is "PreTrip"
/// or "PostTrip", Source is "DriverApp" or "Dispatcher", Result is "Pass",
/// "PassWithDefects", or "Fail". Weather/RoadConditions carry enum names; Visibility and
/// FuelLevel are the enum name or null. The pre-trip section (weather/road/fuel) is populated
/// on PreTrip records, the post-trip section (issues/attestations/signature/fuel-added) on
/// PostTrip records.
/// </summary>
public sealed record VehicleInspectionResponse(
    Guid Id,
    Guid? VehicleId,
    string Unit,
    string Type,
    string DriverName,
    string Source,
    string? EnteredBy,
    string? TripNumber,
    Guid? ManifestId,
    DateTimeOffset PerformedAt,
    int? OdometerKm,
    string Result,
    IReadOnlyList<InspectionChecklistItemResponse> Checklist,
    IReadOnlyList<InspectionDefectResponse> Defects,
    IReadOnlyList<string> Weather,
    string? TemperatureC,
    IReadOnlyList<string> RoadConditions,
    string? Visibility,
    string? RoadAdvisories,
    string? FuelLevel,
    IReadOnlyList<string> Issues,
    IReadOnlyList<bool> Attestations,
    string? DriverSignatureName,
    DateTimeOffset? CertifiedAt,
    bool FuelAdded,
    decimal? FuelLitres,
    decimal? FuelCostCad,
    Guid? GeneratedWorkOrderId,
    DateTimeOffset CreatedAtUtc);

/// <summary>One checklist row. Group is null for post-trip items.</summary>
public sealed record InspectionChecklistItemResponse(string? Group, string Item, bool Passed);

/// <summary>One defect. Severity is "Minor", "Major", or "OutOfService".</summary>
public sealed record InspectionDefectResponse(string Item, string Severity, string? Note);
