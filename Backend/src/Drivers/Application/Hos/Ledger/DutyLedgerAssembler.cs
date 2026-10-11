using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.HoursOfService;
using DutyStatus = NorthernLink.Drivers.Domain.Hos.DutyStatus;
using EngineEvent = NorthernLink.Shared.HoursOfService.DutyEvent;

namespace NorthernLink.Drivers.Application.Hos.Ledger;

/// <summary>
/// Turns a driver's <see cref="DutyDay"/>s into the rule engine's <see cref="DutyLedger"/>.
/// One place, so the status query, the day summaries, the roster batch and the read-side
/// projection feed the engine identically.
///
/// <para>
/// <b>KnownFrom</b> is the start of the driver's earliest day on file. Until the prior-records
/// attestation exists that is all the platform can honestly claim to know, and the engine turns
/// a known-from inside the last 14 days into <c>RecordsIncomplete</c> — blocking, never a free
/// pass. A driver with no day at all gets a known-from of "now": nothing is known, the engine
/// reports <c>NoLedger</c>.
/// </para>
/// </summary>
public static class DutyLedgerAssembler
{
    /// <summary>
    /// How far back a ledger is loaded for an evaluation: the 14-day records window plus a
    /// week so the 7-day cycle and the 36h reset have what they need at the window's edge.
    /// </summary>
    public const int LookbackDays = 21;

    public static readonly HosCalendar Calendar = new();

    public static readonly HosBands Bands = new();

    public static DutyLedger Build(
        Guid driverId,
        IEnumerable<DutyDay> days,
        DateOnly? knownFromDate,
        long asOfUnix)
    {
        var events = new List<EngineEvent>();
        var certified = new List<DateOnly>();

        foreach (var day in days.OrderBy(d => d.LocalDate))
        {
            foreach (var e in day.EffectiveTimeline())
            {
                events.Add(ToEngineEvent(e));
            }

            if (day.IsCertified)
            {
                certified.Add(day.LocalDate);
            }
        }

        var knownFrom = knownFromDate is { } first ? Calendar.DayStartUnix(first) : asOfUnix;

        return new DutyLedger(
            driverId.ToString("D"),
            knownFrom,
            events,
            OtherCarrierDays: [],
            Exceptions: [],
            certified,
            OutOfService: [],
            Malfunctions: []);
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

    public static int? ToDecikm(decimal? km) => km is { } value ? (int)Math.Round(value * 10m, MidpointRounding.AwayFromZero) : null;

    public static HosEvaluation Evaluate(DutyLedger ledger, long asOfUnix) =>
        HosEngine.Evaluate(ledger, asOfUnix, Calendar, Bands);
}
