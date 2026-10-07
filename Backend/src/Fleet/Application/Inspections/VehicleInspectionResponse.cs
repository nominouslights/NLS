namespace NorthernLink.Fleet.Application.Inspections;

/// <summary>
/// The Fleet module's public representation of a vehicle inspection — the shape every
/// frontend consumes. Enum-typed fields travel as their enum names: Type is "PreTrip"
/// or "PostTrip", Source is "DriverApp" or "Dispatcher", Result is "Pass",
/// "PassWithDefects", or "Fail". Weather/RoadConditions carry enum names; Visibility and
/// FuelLevel are the enum name or null. The pre-trip section (weather/road/fuel) is populated
/// on PreTrip records, the post-trip section (issues/attestations/signature/fuel-added) on
/// PostTrip records. The carrier acknowledgement trio is populated only on a report that was
/// signed off under NL-PTI-01 (which only a "Fail" can be), and
/// <see cref="CertificationStatement"/> is the attestation sentence as it read on the day.
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
    DateTimeOffset CreatedAtUtc,
    string? CarrierAcknowledgedBy,
    DateTimeOffset? CarrierAcknowledgedAtUtc,
    string? CarrierAcknowledgementNote,
    string? CertificationStatement,
    string? Location);

/// <summary>
/// One checklist row. Group is null for post-trip items.
///
/// <see cref="State"/> is the NL-PTI-01 tri-state answer — "Ok", "Defect" or "NotApplicable" —
/// and is never null on the wire: a row stored before the tri-state existed reports its
/// EFFECTIVE state, derived from <see cref="Passed"/>, so a client never has to implement that
/// fallback itself. <see cref="Passed"/> is kept alongside it for the screens that predate the
/// change and means "not a defect", which includes a NotApplicable row.
/// </summary>
public sealed record InspectionChecklistItemResponse(
    string? Group,
    string Item,
    bool Passed,
    string State,
    string? Note);

/// <summary>
/// One defect. Severity is "Minor", "Major", or "OutOfService". <see cref="WorkOrderId"/> is the
/// ACTIVE work order the defect is attached to (null when none). <see cref="ResolutionReason"/>
/// is a <c>DefectResolutionReason</c> name and, with <see cref="ResolvedAtUtc"/>, is null while
/// the defect is open.
/// </summary>
public sealed record InspectionDefectResponse(
    string Item,
    string Severity,
    string? Note,
    Guid? WorkOrderId,
    string? ResolutionReason,
    DateTimeOffset? ResolvedAtUtc)
{
    public static InspectionDefectResponse From(Domain.Inspections.InspectionDefect defect) => new(
        defect.Item,
        defect.Severity.ToString(),
        defect.Note,
        defect.WorkOrderId,
        defect.ResolutionReason?.ToString(),
        defect.ResolvedAtUtc);
}
