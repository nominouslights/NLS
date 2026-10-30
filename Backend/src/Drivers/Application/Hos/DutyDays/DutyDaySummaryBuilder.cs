using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Application.Hos.Ledger;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.HoursOfService;
using DutyEvent = NorthernLink.Drivers.Domain.Hos.DutyEvent;

namespace NorthernLink.Drivers.Application.Hos.DutyDays;

/// <summary>
/// Builds <see cref="DutyDaySummaryResponse"/>s for a run of dates from one driver's ledger.
/// The engine reports the 14 days ending at its as-of instant, so a day outside today's window
/// is evaluated with as-of parked at that day's end (which makes it the "yesterday" of that
/// evaluation — complete, with every prior day the cycle needs still in the ledger). One
/// evaluation serves every requested day it covers, so a 14-day calendar costs one run. The
/// compliance records ride along: each summary lists the exceptions the engine honoured on
/// that day and any out-of-service declaration overlapping it.
/// </summary>
public static class DutyDaySummaryBuilder
{
    public static IReadOnlyList<DutyDaySummaryResponse> Build(
        Guid driverId,
        IReadOnlyList<DutyDay> ledgerDays,
        DateOnly? knownFromDate,
        DateOnly from,
        DateOnly to,
        long nowUnix,
        ComplianceRecords? compliance = null)
    {
        compliance ??= ComplianceRecords.None;
        var calendar = DutyLedgerAssembler.Calendar;
        var byDate = ledgerDays.ToDictionary(d => d.LocalDate);
        var summaries = new List<DutyDaySummaryResponse>();

        HosEvaluation? evaluation = null;
        for (var date = to; date >= from; date = date.AddDays(-1))
        {
            if (evaluation is null || evaluation.Days.All(d => d.Date != date))
            {
                var asOf = Math.Min(nowUnix, calendar.DayEndUnix(date));
                var ledger = DutyLedgerAssembler.Build(driverId, ledgerDays, knownFromDate, asOf, compliance);
                evaluation = DutyLedgerAssembler.Evaluate(ledger, asOf);
            }

            var totals = evaluation.Days.FirstOrDefault(d => d.Date == date)
                ?? new DayTotals(date, false, date < calendar.DayOf(nowUnix), 0, 0, 0, 0, 0, 0, false, false, false, HosLimits.DailyOffSeconds, HosLimits.DailyOffSeconds, false);

            byDate.TryGetValue(date, out var day);
            var dayAsOf = Math.Min(nowUnix, calendar.DayEndUnix(date));
            var dayStart = calendar.DayStartUnix(date);
            var dayEnd = calendar.DayEndUnix(date);

            summaries.Add(new DutyDaySummaryResponse(
                driverId,
                date,
                day is not null,
                day?.Id,
                totals,
                day?.PersonalConveyanceKm ?? 0m,
                day?.IsCertified ?? false,
                day?.CertifiedAtUtc,
                day?.RecertificationRequired ?? false,
                day is null ? [] : Segments(day, dayAsOf),
                day is null ? [] : day.Events.OrderBy(e => e.AtUtc).ThenBy(e => e.Sequence).Select(ToResponse).ToList(),
                evaluation.Violations.Where(v => v.Date == date).ToList(),
                ApplyingExceptionsOn(compliance, dayStart, dayEnd),
                OutOfServiceOn(compliance, dayStart, dayEnd),
                HosDisplay.PlanningLabel));
        }

        summaries.Reverse();
        return summaries;
    }

    /// <summary>The exceptions the engine honoured whose span touches the day, oldest first.</summary>
    public static IReadOnlyList<HosExceptionResponse> ApplyingExceptionsOn(ComplianceRecords compliance, long dayStartUnix, long dayEndUnix) =>
        compliance.Exceptions
            .Where(e => e.IsApplying && e.Overlaps(dayStartUnix, dayEndUnix))
            .OrderBy(e => e.EffectiveFromUtc)
            .Select(ComplianceResponseMapper.ToResponse)
            .ToList();

    /// <summary>The out-of-service declaration overlapping the day (the earliest, if several), or null.</summary>
    public static OutOfServiceResponse? OutOfServiceOn(ComplianceRecords compliance, long dayStartUnix, long dayEndUnix) =>
        compliance.OutOfService
            .Where(o => o.Overlaps(dayStartUnix, dayEndUnix))
            .OrderBy(o => o.DeclaredAtUtc)
            .Select(ToResponse)
            .FirstOrDefault();

    public static OutOfServiceResponse ToResponse(DriverOutOfService record) => new(
        record.Id, record.DeclaredAtUtc, record.EffectiveUntilUtc, record.Reason);

    public static DutyEventResponse ToResponse(DutyEvent e) => new(
        e.EventId,
        e.Sequence,
        e.AtUtc,
        e.RecordedAtUtc,
        HosDisplay.DutyToWire(e.Status),
        HosDisplay.SourceToWire(e.Source),
        e.RecordedByUserId,
        e.EnteredBy,
        e.CarrierName,
        e.Note,
        e.OdometerStartKm,
        e.OdometerEndKm,
        e.PcNoPassengersOrCargo,
        e.PcNoTrailer,
        e.PcNoWork,
        e.AmendsEventId,
        e.AmendmentReason,
        e.IsVoided);

    private static IReadOnlyList<DutySegmentResponse> Segments(DutyDay day, long asOfUnix) =>
        DutyDayTimeline.Segments(day, asOfUnix)
            .Select(s => new DutySegmentResponse(
                HosDisplay.DutyToWire(s.Status),
                HosDisplay.SourceToWire(s.Source),
                DateTimeOffset.FromUnixTimeSeconds(s.FromUnix),
                DateTimeOffset.FromUnixTimeSeconds(s.ToUnix),
                s.DurationSeconds,
                s.EventId))
            .ToList();
}
