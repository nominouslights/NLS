using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Shared.HoursOfService;

namespace NorthernLink.Drivers.Application.Hos;

/// <summary>
/// One duty event as the API shows it. <paramref name="Status"/> and <paramref name="Source"/>
/// are <c>HosWire</c> strings. Voided events are included (with <paramref name="IsVoided"/>
/// true and the replacement pointing back via <paramref name="AmendsEventId"/>) so the timeline
/// can render an amendment history; the <c>segments</c> beside it use only effective events.
/// </summary>
public sealed record DutyEventResponse(
    Guid EventId,
    int Sequence,
    DateTimeOffset AtUtc,
    DateTimeOffset RecordedAtUtc,
    string Status,
    string Source,
    Guid? RecordedByUserId,
    string? EnteredBy,
    string? CarrierName,
    string? Note,
    decimal? OdometerStartKm,
    decimal? OdometerEndKm,
    bool PcNoPassengersOrCargo,
    bool PcNoTrailer,
    bool PcNoWork,
    Guid? AmendsEventId,
    string? AmendmentReason,
    bool IsVoided);

/// <summary>A stretch of one status on a day's timeline (effective events only).</summary>
public sealed record DutySegmentResponse(
    string Status,
    string Source,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int DurationSeconds,
    Guid EventId);

/// <summary>
/// One day of a driver's ledger. <paramref name="Totals"/> is the rule engine's own
/// <see cref="DayTotals"/> for that date (seconds, tenths of a kilometre — the TypeScript
/// mirror's shape), <paramref name="Violations"/> the engine's breaches dated that day.
/// <paramref name="HasRecords"/> is false for a day inside the requested range with no
/// <c>DutyDay</c> on file — the totals then describe an unknown or empty day, which the engine
/// treats as records incomplete, never as hours unused. <paramref name="Exceptions"/> are the
/// exceptions in force on the day (the ones the engine honoured — Declared, Acknowledged or
/// Approved); <paramref name="OutOfService"/> is an out-of-service declaration overlapping it.
/// </summary>
public sealed record DutyDaySummaryResponse(
    Guid DriverId,
    DateOnly Date,
    bool HasRecords,
    Guid? DutyDayId,
    DayTotals Totals,
    decimal PersonalConveyanceKm,
    bool Certified,
    DateTimeOffset? CertifiedAtUtc,
    bool RecertificationRequired,
    IReadOnlyList<DutySegmentResponse> Segments,
    IReadOnlyList<DutyEventResponse> Events,
    IReadOnlyList<HosViolation> Violations,
    IReadOnlyList<HosExceptionResponse> Exceptions,
    OutOfServiceResponse? OutOfService,
    string Label);

/// <summary>The out-of-service declaration in force (status) or overlapping a day (summaries): its id, span and reason.</summary>
public sealed record OutOfServiceResponse(Guid Id, DateTimeOffset FromUtc, DateTimeOffset? UntilUtc, string Reason);

/// <summary>
/// A driver's hours-of-service standing right now. <paramref name="Evaluation"/> is the rule
/// engine's <see cref="HosEvaluation"/> verbatim (camelCase, enums as member names, Unix
/// seconds) — the same record the TypeScript mirror produces, so both frontends type it once.
/// <paramref name="RecordsOnFile"/> is true when the ledger covers the 14-day window the
/// regulation requires (the engine reports <c>RecordsIncomplete</c> otherwise);
/// <paramref name="KnownFromUtc"/> is where that coverage starts — the driver's first day on
/// file or the start of the attested prior-records window, whichever is earlier.
/// <paramref name="OutOfService"/> is the declaration in force right now (null when none);
/// <paramref name="EldMalfunctionOpen"/> is true while any ELD malfunction report is open
/// (paper-log mode — a warning, never a block); <paramref name="PriorRecords"/> is the
/// compliance officer's attestation, null until one exists.
/// </summary>
public sealed record HosStatusResponse(
    Guid DriverId,
    string Label,
    string CurrentDuty,
    DateTimeOffset CurrentDutySinceUtc,
    bool RecordsOnFile,
    DateTimeOffset? KnownFromUtc,
    OutOfServiceResponse? OutOfService,
    bool EldMalfunctionOpen,
    PriorRecordsResponse? PriorRecords,
    HosEvaluation Evaluation);

/// <summary>One roster row of <c>GET /api/drivers/hos/status</c>.</summary>
public sealed record HosRosterStatusResponse(
    Guid DriverId,
    HosBand Band,
    int TightestRemainingSeconds,
    bool CanDriveNow,
    IReadOnlyList<BlockingReason> BlockingReasons,
    string CurrentDuty);

/// <summary>Body of <c>POST …/hos/duty-events</c>: 201 on first write, 200 (same body, <paramref name="Created"/> false) on replay.</summary>
public sealed record DutyEventRecordedResponse(Guid EventId, Guid DutyDayId, DateOnly LocalDate, bool Created);

/// <summary>Body of <c>POST …/hos/days/{date}/transcribe</c>. All-or-nothing: on failure nothing was written.</summary>
public sealed record TranscriptionRecordedResponse(Guid DutyDayId, DateOnly LocalDate, int Appended, int AlreadyRecorded);

/// <summary>Body of <c>POST …/hos/duty-events/{eventId}/amend</c>.</summary>
public sealed record DutyEventAmendedResponse(
    Guid OriginalEventId,
    Guid ReplacementEventId,
    Guid DutyDayId,
    DateOnly LocalDate,
    bool RecertificationRequired,
    bool Created);
