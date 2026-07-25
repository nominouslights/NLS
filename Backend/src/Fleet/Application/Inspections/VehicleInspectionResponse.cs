namespace NorthernLink.Fleet.Application.Inspections;

/// <summary>
/// The Fleet module's public representation of a vehicle inspection — the shape every
/// frontend consumes. Enum-typed fields travel as their enum names: Type is "PreTrip"
/// or "PostTrip", Source is "DriverApp" or "PaperTranscription", Result is "Pass",
/// "PassWithDefects", or "Fail".
/// </summary>
public sealed record VehicleInspectionResponse(
    Guid Id,
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
    Guid? GeneratedWorkOrderId,
    DateTimeOffset CreatedAtUtc);

/// <summary>One checklist row. Group is null for post-trip items.</summary>
public sealed record InspectionChecklistItemResponse(string? Group, string Item, bool Passed);

/// <summary>One defect. Severity is "Minor", "Major", or "OutOfService".</summary>
public sealed record InspectionDefectResponse(string Item, string Severity, string? Note);
