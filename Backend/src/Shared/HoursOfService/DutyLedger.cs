using System.Text.Json.Serialization;

namespace NorthernLink.Shared.HoursOfService;

/// <summary>
/// A driver's duty status at an instant. Member spellings are shared with the TypeScript
/// mirror (<c>Dispatcher/lib/hos/engine.ts</c>) and travel on the wire via <see cref="HosWire"/>.
/// </summary>
public enum DutyStatus
{
    OffDuty,
    OnDuty,
    Driving,
    PersonalConveyance,
}

/// <summary>Where a <see cref="DutyEvent"/> came from. Spellings shared with the TS mirror.</summary>
public enum EventSource
{
    DriverApp,
    EldTranscription,
    PaperLog,
    OtherCarrier,
    System,
}

/// <summary>
/// One duty-status change. <paramref name="AtUnix"/> is Unix seconds; <paramref name="Sequence"/>
/// breaks ties between events at the same instant (highest wins). <paramref name="OdometerDecikm"/>
/// is tenths of a kilometre and is required on the event that opens a personal-conveyance
/// segment and on the event that closes it.
/// </summary>
public readonly record struct DutyEvent(
    long AtUnix,
    DutyStatus Status,
    EventSource Source,
    long Sequence,
    int? OdometerDecikm,
    string? Note);

/// <summary>
/// An untimed pre-hire declaration of hours worked for another carrier (s.82(1)(f)). Allowed only
/// for days before the driver's first own event; later rows are flagged
/// (<c>UntimedOtherCarrierHoursAfterOwnRecords</c>) but their hours still count, conservatively.
/// A row with zero on-duty seconds is a declared full day off and earns off-duty credit; a row
/// with on-duty time has unknown timing and earns none.
/// </summary>
public sealed record OtherCarrierDay(
    DateOnly Date,
    int OnDutySeconds,
    int DrivingSeconds,
    string CarrierName);

/// <summary>
/// A driver-declared or compliance-approved exception. The JSON discriminator <c>kind</c> carries
/// the derived type name so the fixtures and the TS mirror share one shape.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(AdverseDrivingConditions), nameof(AdverseDrivingConditions))]
[JsonDerivedType(typeof(Emergency), nameof(Emergency))]
[JsonDerivedType(typeof(OffDutyDeferral), nameof(OffDutyDeferral))]
public abstract record HosException(string Id, long DeclaredAtUnix, string? TripId, string Reason);

/// <summary>
/// s.76: up to two extra hours of driving / on-duty / elapsed time for the shift and day in which
/// it was declared. Must be declared inside a shift; one per shift.
/// </summary>
public sealed record AdverseDrivingConditions(
    string Id,
    long DeclaredAtUnix,
    string? TripId,
    string Reason,
    int ExtensionSeconds) : HosException(Id, DeclaredAtUnix, TripId, Reason);

/// <summary>
/// Violations inside the emergency span are reported with <c>Suppressed = true</c>; totals and
/// ceilings are unchanged. An open span ends with the driving segment in progress when declared.
/// </summary>
public sealed record Emergency(
    string Id,
    long DeclaredAtUnix,
    string? TripId,
    string Reason,
    long? EndedAtUnix) : HosException(Id, DeclaredAtUnix, TripId, Reason);

/// <summary>
/// s.16: defer up to two hours of day one's off-duty time to day two, subject to the day-pair
/// conditions the engine checks (<see cref="HosRule.DeferralConditions"/>).
/// </summary>
public sealed record OffDutyDeferral(
    string Id,
    long DeclaredAtUnix,
    string? TripId,
    string Reason,
    DateOnly DayOne,
    int DeferredSeconds,
    string ApprovedByComplianceOfficer) : HosException(Id, DeclaredAtUnix, TripId, Reason);

/// <summary>An out-of-service declaration (s.91). <paramref name="UntilUnix"/> null = open.</summary>
public sealed record OutOfService(long FromUnix, long? UntilUnix, string Reason);

/// <summary>An ELD malfunction report (s.78.2). Informational to the engine; never blocking.</summary>
public sealed record EldMalfunction(long NoticedAtUnix, string Code, long? RepairedAtUnix);

/// <summary>
/// Everything the engine knows about one driver. <paramref name="KnownFromUnix"/> is the instant
/// from which the records are complete; time before it is unknown, never "free".
/// </summary>
public sealed record DutyLedger(
    string DriverId,
    long KnownFromUnix,
    IReadOnlyList<DutyEvent> Events,
    IReadOnlyList<OtherCarrierDay> OtherCarrierDays,
    IReadOnlyList<HosException> Exceptions,
    IReadOnlyList<DateOnly> CertifiedDays,
    IReadOnlyList<OutOfService> OutOfService,
    IReadOnlyList<EldMalfunction> Malfunctions);
