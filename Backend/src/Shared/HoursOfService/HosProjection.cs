namespace NorthernLink.Shared.HoursOfService;

/// <summary>A planned block of one status, in seconds before margin.</summary>
public sealed record PlannedSegment(DutyStatus Status, int Seconds);

/// <summary>One leg of a plan — typically pre-trip on-duty, driving, post-trip on-duty.</summary>
public sealed record PlannedLeg(long StartUnix, IReadOnlyList<PlannedSegment> Segments, string? TripId);

/// <summary>
/// What the driver is being asked to do. <paramref name="OffDutyFromUnix"/> (default: as-of) is
/// the instant the engine assumes the driver goes off duty before the first leg;
/// <paramref name="BetweenLegs"/> is the status assumed in the gaps between legs.
/// </summary>
public sealed record TripPlan(
    IReadOnlyList<PlannedLeg> Legs,
    long? OffDutyFromUnix,
    DutyStatus BetweenLegs = DutyStatus.OffDuty);

/// <summary>
/// The verdict on a plan. <paramref name="RequiredOffBeforeStartSeconds"/> is the smallest of
/// 0 / 8h / the day's 10h shortfall / 24h / 36h that makes the plan feasible when taken as one
/// off-duty run ending at the first leg's start, or null when none does.
/// </summary>
public sealed record HosProjection(
    bool Feasible,
    HosViolation? FirstBreach,
    long? FirstBreachAtUnix,
    int MarginAppliedSeconds,
    long AssumedOffDutyFromUnix,
    int? RequiredOffBeforeStartSeconds,
    int DailyOffStillNeededAfterPlanSeconds,
    bool DailyOffAchievableAfterPlan,
    HosEvaluation AtPlanEnd,
    IReadOnlyList<HosLedgerProblem> Problems);
