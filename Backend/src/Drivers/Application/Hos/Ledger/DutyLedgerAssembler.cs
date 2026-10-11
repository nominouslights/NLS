using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.HoursOfService;
using DutyStatus = NorthernLink.Drivers.Domain.Hos.DutyStatus;
using EngineEvent = NorthernLink.Shared.HoursOfService.DutyEvent;
using EngineException = NorthernLink.Shared.HoursOfService.HosException;
using EngineOutOfService = NorthernLink.Shared.HoursOfService.OutOfService;
using HosException = NorthernLink.Drivers.Domain.Hos.HosException;

namespace NorthernLink.Drivers.Application.Hos.Ledger;

/// <summary>
/// Turns a driver's <see cref="DutyDay"/>s and compliance records into the rule engine's
/// <see cref="DutyLedger"/>. One place, so the status query, the day summaries, the audit, the
/// roster batch and the read-side projection feed the engine identically.
///
/// <para>
/// <b>KnownFrom</b> is the start of the driver's earliest day on file — or, once a compliance
/// officer has attested the driver's prior records (s.82(1)(f)), the start of the first day the
/// attestation covers (<c>coveredThrough − 13</c>), whichever is earlier. Until then that is
/// all the platform can honestly claim to know, and the engine turns a known-from inside the
/// last 14 days into <c>RecordsIncomplete</c> — blocking, never a free pass. A driver with no
/// day at all and no attestation gets a known-from of "now": nothing is known, the engine
/// reports <c>NoLedger</c>.
/// </para>
///
/// <para>
/// <b>Attested days are fed as declared other-carrier days.</b> The engine flags every completed
/// day after known-from that has neither own events nor a declared other-carrier row as
/// records-incomplete, so moving known-from back on its own would leave the driver blocked. The
/// attestation is exactly the s.82(1)(f) pre-hire declaration the engine's
/// <see cref="OtherCarrierDay"/> models, so each attested day before the driver's first own day
/// that has no <see cref="DutyDay"/> is passed as a zero-hour row named
/// <see cref="PriorRecordsCarrierName"/>. Zero hours is what the engine treats as a declared day
/// off — the attestation carries no hours, so this is the engine's reading of "records on file",
/// not a claim this module makes. Days the ledger does hold use their own events.
/// </para>
/// </summary>
public static class DutyLedgerAssembler
{
    /// <summary>
    /// How far back a ledger is loaded for an evaluation: the 14-day records window plus a
    /// week so the 7-day cycle and the 36h reset have what they need at the window's edge.
    /// </summary>
    public const int LookbackDays = 21;

    /// <summary>The regulation's records window: an attestation through day X covers [X − 13, X].</summary>
    public const int AttestationCoverageDays = 14;

    /// <summary>The carrier name on the synthetic other-carrier rows an attestation produces.</summary>
    public const string PriorRecordsCarrierName = "Prior records attested (on file)";

    public static readonly HosCalendar Calendar = new();

    public static readonly HosBands Bands = new();

    /// <summary>
    /// The first day the ledger knows: the earlier of the driver's first day on file and the
    /// first day an attestation covers. Null when neither exists.
    /// </summary>
    public static DateOnly? KnownFromDate(DateOnly? earliestDay, DateOnly? priorRecordsCoveredThrough)
    {
        DateOnly? attested = priorRecordsCoveredThrough is { } through
            ? through.AddDays(-(AttestationCoverageDays - 1))
            : null;

        return (earliestDay, attested) switch
        {
            (null, null) => null,
            ({ } a, null) => a,
            (null, { } b) => b,
            ({ } a, { } b) => a < b ? a : b,
        };
    }

    public static DutyLedger Build(
        Guid driverId,
        IEnumerable<DutyDay> days,
        DateOnly? knownFromDate,
        long asOfUnix,
        ComplianceRecords? compliance = null)
    {
        compliance ??= ComplianceRecords.None;

        var events = new List<EngineEvent>();
        var certified = new List<DateOnly>();
        var onFile = new HashSet<DateOnly>();
        DateOnly? earliestLoaded = null;

        foreach (var day in days.OrderBy(d => d.LocalDate))
        {
            earliestLoaded ??= day.LocalDate;
            onFile.Add(day.LocalDate);

            foreach (var e in day.EffectiveTimeline())
            {
                events.Add(ToEngineEvent(e));
            }

            if (day.IsCertified)
            {
                certified.Add(day.LocalDate);
            }
        }

        var firstDay = KnownFromDate(knownFromDate ?? earliestLoaded, compliance.PriorRecordsCoveredThroughDate);
        var knownFrom = firstDay is { } first ? Calendar.DayStartUnix(first) : asOfUnix;

        return new DutyLedger(
            driverId.ToString("D"),
            knownFrom,
            events,
            AttestedDays(compliance.PriorRecordsCoveredThroughDate, knownFromDate ?? earliestLoaded, onFile),
            compliance.Exceptions.Select(ToEngineException).Where(e => e is not null).Select(e => e!).ToList(),
            certified,
            compliance.OutOfService.Select(ToEngineSpan).ToList(),
            compliance.Malfunctions.Select(ToEngineMalfunction).ToList());
    }

    /// <summary>
    /// The engine's odometer is one reading per event: the start reading on the event that
    /// opens personal conveyance, the end reading on the event that closes it. Kilometres to one
    /// decimal become tenths of a kilometre.
    /// </summary>
    public static EngineEvent ToEngineEvent(Domain.Hos.DutyEvent e) => new(
        e.AtUtc.ToUnixTimeSeconds(),
        HosDisplay.ToEngine(e.Status),
        HosDisplay.ToEngine(e.Source),
        e.Sequence,
        ToDecikm(e.Status == DutyStatus.PersonalConveyance ? e.OdometerStartKm : e.OdometerEndKm),
        e.Note);

    /// <summary>
    /// The engine's view of an exception, or null when it no longer applies (rejected or
    /// withdrawn). Adverse conditions and an emergency are applied at the instant the conditions
    /// arose (<c>EffectiveFromUtc</c>) — that is what the engine's "declared inside the shift"
    /// test means — not at the instant the record was written. A deferral's day one is the
    /// America/Winnipeg day its effective-from falls in.
    /// </summary>
    public static EngineException? ToEngineException(HosException e)
    {
        if (!e.IsApplying)
        {
            return null;
        }

        var id = e.Id.ToString("D");
        var from = e.EffectiveFromUtc.ToUnixTimeSeconds();
        return e.Kind switch
        {
            HosExceptionKind.AdverseDrivingConditions => new AdverseDrivingConditions(
                id, from, e.TripId?.ToString("D"), e.Reason, (int)(e.ExtraDriving?.TotalSeconds ?? 0)),
            HosExceptionKind.Emergency => new Emergency(
                id, from, e.TripId?.ToString("D"), e.Reason, e.EffectiveToUtc?.ToUnixTimeSeconds()),
            HosExceptionKind.OffDutyDeferral => new OffDutyDeferral(
                id,
                e.DeclaredAtUtc.ToUnixTimeSeconds(),
                e.TripId?.ToString("D"),
                e.Reason,
                Calendar.DayOf(from),
                (int)(e.DeferredOffDuty?.TotalSeconds ?? 0),
                e.ApprovedByUserId?.ToString("D") ?? string.Empty),
            _ => null,
        };
    }

    public static EngineOutOfService ToEngineSpan(DriverOutOfService record) => new(
        record.DeclaredAtUtc.ToUnixTimeSeconds(),
        record.EffectiveUntilUtc?.ToUnixTimeSeconds(),
        record.Reason);

    public static EldMalfunction ToEngineMalfunction(EldMalfunctionReport report) => new(
        report.NoticedAtUtc.ToUnixTimeSeconds(),
        report.Code,
        report.ResolvedAtUtc?.ToUnixTimeSeconds());

    public static int? ToDecikm(decimal? km) => km is { } value ? (int)Math.Round(value * 10m, MidpointRounding.AwayFromZero) : null;

    public static HosEvaluation Evaluate(DutyLedger ledger, long asOfUnix) =>
        HosEngine.Evaluate(ledger, asOfUnix, Calendar, Bands);

    /// <summary>True when <paramref name="date"/> lies inside the attested window [through − 13, through].</summary>
    public static bool IsAttestedDay(DateOnly date, DateOnly? priorRecordsCoveredThrough) =>
        priorRecordsCoveredThrough is { } through
        && date <= through
        && date >= through.AddDays(-(AttestationCoverageDays - 1));

    private static List<OtherCarrierDay> AttestedDays(DateOnly? coveredThrough, DateOnly? earliestOwnDay, HashSet<DateOnly> onFile)
    {
        var rows = new List<OtherCarrierDay>();
        if (coveredThrough is not { } through)
        {
            return rows;
        }

        for (var date = through.AddDays(-(AttestationCoverageDays - 1)); date <= through; date = date.AddDays(1))
        {
            // Only before the driver's own records begin, and only where no day is on file: the
            // engine flags an untimed declaration dated after own records as a ledger problem,
            // and a day the ledger holds speaks for itself.
            if ((earliestOwnDay is { } own && date >= own) || onFile.Contains(date))
            {
                continue;
            }

            rows.Add(new OtherCarrierDay(date, 0, 0, PriorRecordsCarrierName));
        }

        return rows;
    }
}
