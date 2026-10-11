using NorthernLink.Shared.HoursOfService;

namespace NorthernLink.Drivers.Application.Hos.Compliance;

/// <summary>
/// An hours-of-service exception as the API shows it. <paramref name="Kind"/> is the
/// <c>HosWire</c> kind string ("Adverse driving conditions" / "Emergency" / "Off-duty deferral
/// (CO approved)"); <paramref name="Status"/> the status member name (Declared, Acknowledged,
/// Approved, Rejected, Withdrawn). <paramref name="Applying"/> is true while the rule engine
/// honours it. <paramref name="ExtraDrivingMinutes"/> is set for adverse conditions only,
/// <paramref name="DeferredOffDutyMinutes"/> for a deferral only.
/// </summary>
public sealed record HosExceptionResponse(
    Guid Id,
    Guid DriverId,
    string Kind,
    string Status,
    bool Applying,
    Guid? TripId,
    DateTimeOffset DeclaredAtUtc,
    Guid DeclaredByUserId,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc,
    string Reason,
    string? Location,
    bool NotKnownBeforeDeparture,
    int? ExtraDrivingMinutes,
    int? DeferredOffDutyMinutes,
    bool EldRemarkRecorded,
    DateTimeOffset? DispatchAcknowledgedAtUtc,
    Guid? DispatchAcknowledgedByUserId,
    DateTimeOffset? ApprovedAtUtc,
    Guid? ApprovedByUserId,
    DateTimeOffset? ClosedAtUtc,
    Guid? ClosedByUserId,
    string? ClosureNote);

/// <summary>
/// An out-of-service declaration as the API shows it. <paramref name="Status"/> is Active or
/// Lifted; <paramref name="Active"/> is whether it is in force right now (an Active record
/// whose <paramref name="UntilUtc"/> has passed is not). <paramref name="EffectiveUntilUtc"/>
/// is the lift instant once lifted, else <paramref name="UntilUtc"/> (null = until lifted).
/// </summary>
public sealed record OutOfServiceRecordResponse(
    Guid Id,
    Guid DriverId,
    string Status,
    bool Active,
    DateTimeOffset DeclaredAtUtc,
    DateTimeOffset RecordedAtUtc,
    Guid RecordedByUserId,
    DateTimeOffset? UntilUtc,
    DateTimeOffset? EffectiveUntilUtc,
    string Reason,
    string? Evidence,
    DateTimeOffset? LiftedAtUtc,
    Guid? LiftedByUserId,
    string? LiftReason);

/// <summary>An ELD malfunction report as the API shows it. <paramref name="Status"/> is Open or Resolved.</summary>
public sealed record EldMalfunctionResponse(
    Guid Id,
    Guid DriverId,
    Guid? VehicleId,
    string Code,
    string Status,
    DateTimeOffset NoticedAtUtc,
    DateTimeOffset ReportedAtUtc,
    Guid ReportedByUserId,
    string Description,
    DateTimeOffset? ResolvedAtUtc,
    Guid? ResolvedByUserId,
    string? ResolutionNote);

/// <summary>
/// A compliance finding as the API shows it. <paramref name="Rule"/> is an <c>HosRule</c> name
/// or one of Falsification / Tampering / TwoRecords / OutOfServiceDeclared;
/// <paramref name="Severity"/>, <paramref name="Status"/> and <paramref name="Source"/> are
/// member names.
/// </summary>
public sealed record ComplianceFindingResponse(
    Guid Id,
    Guid DriverId,
    DateOnly LocalDate,
    string Rule,
    string Severity,
    string Status,
    string Source,
    string Description,
    string? FollowUpNote,
    DateTimeOffset OpenedAtUtc,
    Guid OpenedByUserId,
    DateTimeOffset? ClosedAtUtc,
    Guid? ClosedByUserId);

/// <summary>The driver's prior-records attestation (s.82(1)(f)); absent from a status response until a compliance officer records one.</summary>
public sealed record PriorRecordsResponse(
    DateOnly CoveredThroughDate,
    DateTimeOffset AttestedAtUtc,
    Guid AttestedByUserId,
    string? Note,
    bool WorksForOtherCarriers);

/// <summary>Other-carrier work recorded on a day: one row per carrier name, with how many effective events named it.</summary>
public sealed record HosAuditOtherCarrierRow(string CarrierName, int EventCount);

/// <summary>
/// One America/Winnipeg day of the compliance audit (<c>GET …/hos/audit</c>): the day's records
/// at a glance plus everything that bears on it. <paramref name="Sources"/> are the distinct
/// <c>HosWire</c> source strings of the day's effective events; <paramref name="Totals"/> and
/// <paramref name="Violations"/> are the rule engine's; <paramref name="Exceptions"/> are the
/// exceptions in force on the day (any status, so a rejected one still shows);
/// <paramref name="OutOfService"/> is a declaration overlapping the day;
/// <paramref name="EldMalfunctionOpen"/> whether an ELD malfunction was unresolved during it;
/// <paramref name="PriorRecordsAttested"/> whether the day lies inside the attested window.
/// </summary>
public sealed record HosAuditDayResponse(
    Guid DriverId,
    DateOnly Date,
    bool HasRecords,
    int EventCount,
    IReadOnlyList<string> Sources,
    bool Certified,
    DateTimeOffset? CertifiedAtUtc,
    bool RecertificationRequired,
    DayTotals Totals,
    decimal PersonalConveyanceKm,
    IReadOnlyList<HosViolation> Violations,
    IReadOnlyList<HosExceptionResponse> Exceptions,
    OutOfServiceRecordResponse? OutOfService,
    bool EldMalfunctionOpen,
    IReadOnlyList<HosAuditOtherCarrierRow> OtherCarrier,
    bool PriorRecordsAttested,
    IReadOnlyList<ComplianceFindingResponse> Findings,
    string Label);
