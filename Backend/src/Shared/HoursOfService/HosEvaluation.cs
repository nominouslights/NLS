namespace NorthernLink.Shared.HoursOfService;

/// <summary>
/// The rules the engine can report. Member names are the violation ids on the wire, verbatim, and
/// their order is the tie-break order wherever two rules fire at the same instant.
/// </summary>
public enum HosRule
{
    DailyDriving13h,
    DailyOnDuty14h,
    ShiftDriving13h,
    ShiftOnDuty14h,
    ShiftElapsed16h,
    DailyOffDuty10h,
    Rest24hIn14d,
    Cycle1_70hIn7d,
    DeferralConditions,
    PcExceeds75km,
    PcOdometerMissing,
    PcWhileOutOfService,
    DroveWhileOutOfService,
    RodsNotCertified,
    RecordsIncomplete14d,
    AdverseDeclaredOutsideShift,
    ConflictingSources,
}

/// <summary>
/// One breach. <paramref name="AtUnix"/> is the instant the figure reached the ceiling (the last
/// lawful second); <paramref name="FigureSeconds"/> is the figure at the end of the last offending
/// piece. For the personal-conveyance rule the figure and limit are tenths of a kilometre.
/// </summary>
public sealed record HosViolation(
    HosRule Rule,
    DateOnly Date,
    long AtUnix,
    int FigureSeconds,
    int LimitSeconds,
    bool Suppressed,
    string? SuppressedByExceptionId,
    string Message);

/// <summary>Why the driver may not drive right now, in the order they are reported.</summary>
public enum BlockingReason
{
    NoLedger,
    RecordsIncomplete,
    OutOfService,
    Rest24Expired,
    CycleExhausted,
    DailyDrivingExhausted,
    DailyOnDutyExhausted,
    ShiftDrivingExhausted,
    ShiftOnDutyExhausted,
    ShiftWindowClosed,
}

public enum HosBand
{
    Ontime,
    Soon,
    Over,
    Off,
}

/// <summary>Per-day totals. On-duty includes driving and declared other-carrier hours.</summary>
public sealed record DayTotals(
    DateOnly Date,
    bool Known,
    bool Complete,
    int DrivingSeconds,
    int OnDutySeconds,
    int OtherCarrierOnDutySeconds,
    int OffDutySeconds,
    int RawOffDutySeconds,
    int PcDecikm,
    bool HasOwnEvents,
    bool Certified,
    bool EightHourConsecutiveMet,
    int DailyOffRequiredSeconds,
    int OffDutyShortfallSeconds,
    bool DailyOffMet);

/// <summary>
/// The current shift: everything since the end of the last off-duty run of 8h or more. A null
/// <paramref name="StartUnix"/> means the driver is between shifts (an open run of 8h or more).
/// </summary>
public sealed record ShiftWindow(
    long? StartUnix,
    int DrivingSeconds,
    int OnDutySeconds,
    int ElapsedSeconds,
    int DrivingCeilingSeconds,
    int OnDutyCeilingSeconds,
    int ElapsedCeilingSeconds,
    int AdverseExtensionSeconds,
    long? MustStopDrivingByUnix);

public sealed record CycleState(
    int OnDutySeconds,
    int LimitSeconds,
    int RemainingSeconds,
    DateOnly WindowStartDate,
    long? LastResetEndUnix,
    bool RecordsIncomplete);

public sealed record Rest24State(
    bool Satisfied,
    long? QualifyingRunEndUnix,
    long? ExpiresAtUnix,
    int BestOffSeconds,
    bool RecordsIncomplete);

public sealed record NextRequiredOffDuty(
    long MustBeOffByUnix,
    HosRule Reason,
    int MinimumSeconds,
    int DailyShortfallSeconds);

/// <summary>A defect in the ledger itself, as opposed to a breach of the regulation.</summary>
public sealed record HosLedgerProblem(string Code, string Message, long? AtUnix);

public sealed record HosEvaluation(
    long AsOfUnix,
    DutyStatus CurrentStatus,
    long CurrentStatusSinceUnix,
    IReadOnlyList<DayTotals> Days,
    DayTotals Today,
    int RemainingDrivingTodaySeconds,
    int RemainingOnDutyTodaySeconds,
    ShiftWindow Shift,
    CycleState Cycle,
    Rest24State Rest24,
    int PcTodayDecikm,
    int PcRemainingDecikm,
    IReadOnlyList<HosViolation> Violations,
    bool CanDriveNow,
    IReadOnlyList<BlockingReason> BlockingReasons,
    NextRequiredOffDuty? NextRequiredOffDuty,
    int TightestRemainingSeconds,
    HosBand Band,
    IReadOnlyList<HosLedgerProblem> LedgerProblems);
