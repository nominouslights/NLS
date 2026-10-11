using NorthernLink.Drivers.Domain.Hos.Events;
using NorthernLink.Shared.HoursOfService;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Hos;

/// <summary>
/// One driver's duty record for one America/Winnipeg calendar day — the platform's <b>planning
/// ledger and ELD mirror</b>, never a second record of duty status (the RODS lives on the bus's
/// ELD, and keeping two is itself a violation). A unique index on
/// (tenant, driver, <see cref="LocalDate"/>) makes "two records for a day" impossible to store.
///
/// <para>
/// <see cref="Events"/> is append-only jsonb. <see cref="Append"/> is idempotent on
/// <c>EventId</c> — a replayed Driver Field App queue item is recognised, changes nothing and
/// raises nothing, which is what keeps it clear of <c>ModuleDbContext</c>'s eventless-write
/// guard (an unchanged aggregate is never Modified, so the guard never sees it).
/// <see cref="Amend"/> never edits in place: it voids the original and appends the replacement
/// with a reason, and a certified day flips to <see cref="RecertificationRequired"/>.
/// <see cref="Certify"/> is the driver's own act (s.84) — the handler proves the caller IS this
/// driver; the aggregate only refuses a future day or an open personal-conveyance segment.
/// </para>
///
/// <para>
/// The rules enforced here are the ones that make a timeline <i>well-formed</i>: inside the day,
/// not in the future, a real status change, and personal conveyance opened with an odometer and
/// the three attestations and closed with an odometer that did not go backwards. Whether the
/// hours themselves are lawful is the rule engine's job (<c>HosEngine.Evaluate</c>) — logging an
/// over-limit day is the point of a compliance ledger, exactly as with <see cref="HosLogEntry"/>.
/// The one engine-adjacent rule kept here is the 75.0 km personal-conveyance limit: when a
/// closing odometer takes the day past it, the aggregate appends a <see cref="DutyEventSource.System"/>
/// on-duty event at the linearly interpolated instant the allowance ran out (mirroring what a
/// compliant ELD does), so the stored timeline, the engine, and every consumer agree.
/// </para>
/// </summary>
public sealed class DutyDay : AggregateRoot, ITenantScoped
{
    /// <summary>The regulation's day boundaries, America/Winnipeg. Resolved once per process.</summary>
    private static readonly HosCalendar DefaultCalendar = new();

    /// <summary>The note stamped on the system-minted on-duty switch at the personal-conveyance limit.</summary>
    public const string PcLimitReachedNote =
        "Personal conveyance reached the 75.0 km daily limit; the remainder of the drive is on duty (automatic, mirrors a compliant ELD).";

    private const decimal PcDailyLimitKm = HosLimits.PcDailyLimitDecikm / 10m;

    private DutyDay()
    {
        // EF Core materialization only.
    }

    public Guid TenantId { get; private set; }
    public Guid DriverId { get; private set; }
    public DateOnly LocalDate { get; private set; }

    /// <summary>Every event ever appended, voided ones included, in append order.</summary>
    public List<DutyEvent> Events { get; private set; } = [];

    /// <summary>
    /// The sequence of the last event appended — a scalar column that moves on every append or
    /// amendment. Load-bearing, not bookkeeping: EF tracks the jsonb events as owned entries, so
    /// a change inside the array alone leaves the day's own row Unchanged and the audit pipeline
    /// (version bump, journal row, outbox event) would never see the write. Every mutation
    /// touches this column, so the aggregate is always Modified when its timeline is.
    /// </summary>
    public int LastSequence { get; private set; }

    public DateTimeOffset? CertifiedAtUtc { get; private set; }
    public Guid? CertifiedByUserId { get; private set; }

    /// <summary>True when an amendment landed after certification; cleared by re-certifying.</summary>
    public bool RecertificationRequired { get; private set; }

    /// <summary>
    /// Kilometres of personal conveyance credited to the day from closed segments — derived
    /// from the effective timeline after every change and stored so the roster can read it
    /// without replaying the events. Never exceeds 75.0: past the limit the remainder is on duty.
    /// </summary>
    public decimal PersonalConveyanceKm { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsCertified => CertifiedAtUtc is not null && !RecertificationRequired;

    /// <summary>
    /// Opens an empty day. Raises nothing on its own — a day only becomes persistable once
    /// <see cref="Append"/> has recorded its first event, which is the only way a day comes to
    /// exist.
    /// </summary>
    public static DutyDay Open(Guid tenantId, Guid driverId, DateOnly localDate, DateTimeOffset now) => new()
    {
        TenantId = tenantId,
        DriverId = driverId,
        LocalDate = localDate,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    /// <summary>The non-voided events in timeline order (instant, then append sequence).</summary>
    public IReadOnlyList<DutyEvent> EffectiveTimeline() => EffectiveTimeline(Events);

    /// <summary>
    /// Records a duty change. Idempotent on <c>EventId</c>: an id already on the day (voided or
    /// not) returns <see cref="DutyAppendOutcome.AlreadyRecorded"/> with no state change and no
    /// domain event. Every other outcome either records the event (raising
    /// <see cref="DutyEventAppendedDomainEvent"/>) or fails validation with no state change.
    /// </summary>
    public Result<DutyAppendOutcome> Append(DutyEventDraft draft, DateTimeOffset now, HosCalendar? calendar = null)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (Events.Any(e => e.EventId == draft.EventId))
        {
            return Result.Success(DutyAppendOutcome.AlreadyRecorded);
        }

        if (draft.Source == DutyEventSource.System)
        {
            return Result.Failure<DutyAppendOutcome>(HosErrors.SystemSourceReserved);
        }

        var timeline = EffectiveTimeline();
        var validation = Validate(draft, timeline, now, calendar ?? DefaultCalendar);
        if (validation.IsFailure)
        {
            return Result.Failure<DutyAppendOutcome>(validation.Error);
        }

        var appended = Add(draft, now, amendsEventId: null, amendmentReason: null);
        Raise(new DutyEventAppendedDomainEvent(Id, DriverId, TenantId, LocalDate, appended.EventId));

        AfterTimelineChanged(appended, now);
        return Result.Success(DutyAppendOutcome.Recorded);
    }

    /// <summary>
    /// Corrects an event: the original is voided (kept for audit) and <paramref name="replacement"/>
    /// is appended carrying the link and the reason. The replacement is validated against the
    /// timeline as it would be without the original. A system-minted on-duty switch derived from
    /// the original (the 75 km crossing) is voided with it and re-derived from the replacement.
    /// On a certified day this sets <see cref="RecertificationRequired"/>. Idempotent on the
    /// replacement's <c>EventId</c>.
    /// </summary>
    public Result<DutyAppendOutcome> Amend(
        Guid amendsEventId,
        DutyEventDraft replacement,
        string? reason,
        DateTimeOffset now,
        HosCalendar? calendar = null)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        if (Events.Any(e => e.EventId == replacement.EventId))
        {
            return Result.Success(DutyAppendOutcome.AlreadyRecorded);
        }

        var originalIndex = Events.FindIndex(e => e.EventId == amendsEventId);
        if (originalIndex < 0)
        {
            return Result.Failure<DutyAppendOutcome>(HosErrors.EventNotFound);
        }

        if (Events[originalIndex].IsVoided)
        {
            return Result.Failure<DutyAppendOutcome>(HosErrors.EventAlreadyVoided);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure<DutyAppendOutcome>(HosErrors.AmendmentReasonRequired);
        }

        if (replacement.Source == DutyEventSource.System)
        {
            return Result.Failure<DutyAppendOutcome>(HosErrors.SystemSourceReserved);
        }

        // The timeline the replacement must fit: without the original and without any system
        // switch the original produced (it will be re-derived from the replacement).
        var derived = DerivedSystemEventsAround(originalIndex);
        var excluded = new HashSet<int>(derived) { originalIndex };
        var timeline = EffectiveTimeline(Events.Where((_, i) => !excluded.Contains(i)));

        var validation = Validate(replacement, timeline, now, calendar ?? DefaultCalendar);
        if (validation.IsFailure)
        {
            return Result.Failure<DutyAppendOutcome>(validation.Error);
        }

        foreach (var index in excluded)
        {
            Events[index] = Events[index] with { IsVoided = true };
        }

        var appended = Add(replacement, now, amendsEventId, reason.Trim());
        if (CertifiedAtUtc is not null)
        {
            RecertificationRequired = true;
        }

        Raise(new DutyEventAmendedDomainEvent(Id, DriverId, TenantId, LocalDate, amendsEventId, appended.EventId));

        AfterTimelineChanged(appended, now);
        return Result.Success(DutyAppendOutcome.Recorded);
    }

    /// <summary>
    /// The driver certifies the day (s.84). The caller proves the actor IS this driver; here the
    /// day must have started, hold at least one effective event, and not end in an open
    /// personal-conveyance segment. Re-certifying an already-certified day with nothing to
    /// re-certify is a no-op (no event, no write).
    /// </summary>
    public Result Certify(Guid driverUserId, DateTimeOffset now, HosCalendar? calendar = null)
    {
        var cal = calendar ?? DefaultCalendar;
        if (cal.DayStartUnix(LocalDate) > now.ToUnixTimeSeconds())
        {
            return Result.Failure(HosErrors.DayInFuture);
        }

        var timeline = EffectiveTimeline();
        if (timeline.Count == 0)
        {
            return Result.Failure(HosErrors.NothingToCertify);
        }

        if (timeline[^1].Status == DutyStatus.PersonalConveyance)
        {
            return Result.Failure(HosErrors.PcSegmentOpen);
        }

        if (IsCertified)
        {
            return Result.Success();
        }

        CertifiedAtUtc = now;
        CertifiedByUserId = driverUserId;
        RecertificationRequired = false;
        UpdatedAtUtc = now;

        Raise(new DutyDayCertifiedDomainEvent(Id, DriverId, TenantId, LocalDate, driverUserId));
        return Result.Success();
    }

    // ---- Validation ---------------------------------------------------------------------

    private Result Validate(DutyEventDraft draft, IReadOnlyList<DutyEvent> timeline, DateTimeOffset now, HosCalendar calendar)
    {
        if (!Enum.IsDefined(draft.Status))
        {
            return Result.Failure(HosErrors.InvalidDuty);
        }

        if (!Enum.IsDefined(draft.Source))
        {
            return Result.Failure(HosErrors.InvalidSource);
        }

        if (draft.Source is DutyEventSource.EldTranscription or DutyEventSource.PaperLog
            && string.IsNullOrWhiteSpace(draft.EnteredBy))
        {
            return Result.Failure(HosErrors.EnteredByRequired);
        }

        if (draft.Source == DutyEventSource.OtherCarrier && string.IsNullOrWhiteSpace(draft.CarrierName))
        {
            return Result.Failure(HosErrors.CarrierNameRequired);
        }

        if (draft.OdometerStartKm < 0 || draft.OdometerEndKm < 0)
        {
            return Result.Failure(HosErrors.OdometerNegative);
        }

        var at = draft.AtUtc.ToUnixTimeSeconds();
        if (at < calendar.DayStartUnix(LocalDate) || at >= calendar.DayEndUnix(LocalDate))
        {
            return Result.Failure(HosErrors.EventOutsideDay);
        }

        if (at > now.ToUnixTimeSeconds() + HosLimits.FutureSkewSeconds)
        {
            return Result.Failure(HosErrors.EventInFuture);
        }

        var previous = PreviousAt(timeline, draft.AtUtc);
        if (previous is not null && previous.Status == draft.Status)
        {
            return Result.Failure(HosErrors.NoStatusChange);
        }

        if (draft.Status == DutyStatus.PersonalConveyance)
        {
            if (draft.OdometerStartKm is null)
            {
                return Result.Failure(HosErrors.PcOdometerRequired);
            }

            if (!(draft.PcNoPassengersOrCargo && draft.PcNoTrailer && draft.PcNoWork))
            {
                return Result.Failure(HosErrors.PcAttestationRequired);
            }

            if (PersonalConveyanceKmOf(timeline) >= PcDailyLimitKm)
            {
                return Result.Failure(HosErrors.PcAllowanceExhausted);
            }
        }

        if (previous?.Status == DutyStatus.PersonalConveyance)
        {
            if (draft.OdometerEndKm is null)
            {
                return Result.Failure(HosErrors.PcOdometerEndRequired);
            }

            if (draft.OdometerEndKm < previous.OdometerStartKm)
            {
                return Result.Failure(HosErrors.PcOdometerDecreasing);
            }
        }

        return Result.Success();
    }

    // ---- Mutation -------------------------------------------------------------------------

    private DutyEvent Add(DutyEventDraft draft, DateTimeOffset now, Guid? amendsEventId, string? amendmentReason)
    {
        var appended = new DutyEvent
        {
            EventId = draft.EventId,
            Sequence = NextSequence(),
            AtUtc = draft.AtUtc,
            RecordedAtUtc = now,
            Status = draft.Status,
            Source = draft.Source,
            RecordedByUserId = draft.RecordedByUserId,
            EnteredBy = Clean(draft.EnteredBy),
            CarrierName = Clean(draft.CarrierName),
            Note = Clean(draft.Note),
            OdometerStartKm = RoundKm(draft.OdometerStartKm),
            OdometerEndKm = RoundKm(draft.OdometerEndKm),
            PcNoPassengersOrCargo = draft.PcNoPassengersOrCargo,
            PcNoTrailer = draft.PcNoTrailer,
            PcNoWork = draft.PcNoWork,
            AmendsEventId = amendsEventId,
            AmendmentReason = amendmentReason,
        };

        Events.Add(appended);
        UpdatedAtUtc = now;
        return appended;
    }

    /// <summary>
    /// After an append or amendment: re-derive the personal-conveyance figure and, when the
    /// event just added closed a segment that carried the day past 75.0 km, mint the on-duty
    /// switch at the crossing instant.
    /// </summary>
    private void AfterTimelineChanged(DutyEvent added, DateTimeOffset now)
    {
        var timeline = EffectiveTimeline();
        var index = timeline.ToList().FindIndex(e => e.EventId == added.EventId);
        var opener = index > 0 ? timeline[index - 1] : null;

        if (opener is { Status: DutyStatus.PersonalConveyance, OdometerStartKm: { } startKm }
            && added.OdometerEndKm is { } endKm
            && endKm >= startKm)
        {
            var before = PersonalConveyanceKmOf(timeline.Where(e => e.EventId != added.EventId && e.EventId != opener.EventId).ToList());
            var allowance = PcDailyLimitKm - before;
            var segmentKm = endKm - startKm;
            var openAt = opener.AtUtc.ToUnixTimeSeconds();
            var closeAt = added.AtUtc.ToUnixTimeSeconds();

            // A segment of under two seconds cannot host a crossing instant strictly inside it;
            // the engine's own reclassification still covers that (physically impossible) case.
            if (segmentKm > allowance && allowance > 0 && closeAt - openAt >= 2)
            {
                var crossing = openAt + (long)Math.Floor((closeAt - openAt) * allowance / segmentKm);
                crossing = Math.Clamp(crossing, openAt + 1, closeAt - 1);

                var system = new DutyEvent
                {
                    EventId = Guid.NewGuid(),
                    Sequence = NextSequence(),
                    AtUtc = DateTimeOffset.FromUnixTimeSeconds(crossing),
                    RecordedAtUtc = now,
                    Status = DutyStatus.OnDuty,
                    Source = DutyEventSource.System,
                    Note = PcLimitReachedNote,
                    OdometerEndKm = RoundKm(startKm + allowance),
                };

                Events.Add(system);
                Raise(new DutyEventAppendedDomainEvent(Id, DriverId, TenantId, LocalDate, system.EventId));
                timeline = EffectiveTimeline();
            }
        }

        PersonalConveyanceKm = PersonalConveyanceKmOf(timeline);
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Indexes (into <see cref="Events"/>) of the system-minted events that exist only because
    /// of the event at <paramref name="originalIndex"/>: a 75 km switch sitting right after it
    /// when it opened personal conveyance, or right before it when it closed the segment.
    /// </summary>
    private List<int> DerivedSystemEventsAround(int originalIndex)
    {
        var result = new List<int>();
        var timeline = EffectiveTimeline();
        var position = timeline.ToList().FindIndex(e => e.EventId == Events[originalIndex].EventId);
        if (position < 0)
        {
            return result;
        }

        if (position + 1 < timeline.Count && timeline[position + 1].Source == DutyEventSource.System)
        {
            result.Add(Events.FindIndex(e => e.EventId == timeline[position + 1].EventId));
        }

        if (position - 1 >= 0 && timeline[position - 1].Source == DutyEventSource.System)
        {
            result.Add(Events.FindIndex(e => e.EventId == timeline[position - 1].EventId));
        }

        return result;
    }

    // ---- Pure helpers ---------------------------------------------------------------------

    private static IReadOnlyList<DutyEvent> EffectiveTimeline(IEnumerable<DutyEvent> events) =>
        events
            .Where(e => !e.IsVoided)
            .OrderBy(e => e.AtUtc)
            .ThenBy(e => e.Sequence)
            .ToList();

    /// <summary>The effective event in force at <paramref name="at"/> (same instant counts).</summary>
    private static DutyEvent? PreviousAt(IReadOnlyList<DutyEvent> timeline, DateTimeOffset at)
    {
        DutyEvent? previous = null;
        foreach (var e in timeline)
        {
            if (e.AtUtc <= at)
            {
                previous = e;
            }
            else
            {
                break;
            }
        }

        return previous;
    }

    /// <summary>
    /// Kilometres credited as personal conveyance on an effective timeline: for each
    /// personal-conveyance opener, the next event's end odometer minus the start. An open
    /// segment (no closer yet) or a closer without an odometer contributes nothing.
    /// </summary>
    public static decimal PersonalConveyanceKmOf(IReadOnlyList<DutyEvent> timeline)
    {
        decimal total = 0;
        for (var i = 0; i < timeline.Count - 1; i++)
        {
            var opener = timeline[i];
            if (opener.Status != DutyStatus.PersonalConveyance || opener.OdometerStartKm is not { } start)
            {
                continue;
            }

            if (timeline[i + 1].OdometerEndKm is { } end && end >= start)
            {
                total += end - start;
            }
        }

        return total;
    }

    private int NextSequence() => ++LastSequence;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal? RoundKm(decimal? km) =>
        km is { } value ? Math.Round(value, 1, MidpointRounding.AwayFromZero) : null;
}
