using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Application.Hos.DutyDays;
using NorthernLink.Drivers.Application.Hos.DutyDays.GetRange;
using NorthernLink.Drivers.Application.Hos.Ledger;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Audit;

/// <summary>
/// Builds the audit rows from the same day summaries the calendar uses (engine totals and
/// violations, certification, the honoured exceptions, the out-of-service overlap) and adds
/// what only an auditor needs: every exception touching the day regardless of status, the
/// day's sources and other-carrier rows, whether an ELD malfunction was open, whether the day
/// is inside the attested prior-records window, and the findings against it.
/// </summary>
public sealed class GetHosAuditQueryHandler(
    IDutyDayRepository repository,
    IComplianceRecordsReader compliance,
    TimeProvider clock)
    : IQueryHandler<GetHosAuditQuery, IReadOnlyList<HosAuditDayResponse>>
{
    public async Task<Result<IReadOnlyList<HosAuditDayResponse>>> Handle(GetHosAuditQuery query, CancellationToken cancellationToken)
    {
        if (query.From > query.To || query.To.DayNumber - query.From.DayNumber >= GetDutyDaysQueryHandler.MaxRangeDays)
        {
            return Result.Failure<IReadOnlyList<HosAuditDayResponse>>(GetDutyDaysQueryHandler.RangeInvalid);
        }

        var nowUnix = clock.GetUtcNow().ToUnixTimeSeconds();
        var calendar = DutyLedgerAssembler.Calendar;
        var today = calendar.DayOf(nowUnix);
        var to = query.To > today ? today : query.To;
        var loadFrom = query.From.AddDays(-DutyLedgerAssembler.LookbackDays);

        var records = await compliance.LoadAsync(
            query.DriverId, DateTimeOffset.FromUnixTimeSeconds(calendar.DayStartUnix(loadFrom)), cancellationToken);
        if (records is null)
        {
            return Result.Failure<IReadOnlyList<HosAuditDayResponse>>(DriverErrors.NotFound);
        }

        if (query.From > to)
        {
            return Result.Success<IReadOnlyList<HosAuditDayResponse>>([]);
        }

        var days = await repository.LoadRangeAsync(query.DriverId, loadFrom, to, cancellationToken);
        var knownFrom = await repository.EarliestLocalDateAsync(query.DriverId, cancellationToken);
        var summaries = DutyDaySummaryBuilder.Build(query.DriverId, days, knownFrom, query.From, to, nowUnix, records);

        return Result.Success(Build(summaries, days, records, clock.GetUtcNow()));
    }

    public static IReadOnlyList<HosAuditDayResponse> Build(
        IReadOnlyList<DutyDaySummaryResponse> summaries,
        IReadOnlyList<DutyDay> days,
        ComplianceRecords records,
        DateTimeOffset now)
    {
        var calendar = DutyLedgerAssembler.Calendar;
        var byDate = days.ToDictionary(d => d.LocalDate);
        var rows = new List<HosAuditDayResponse>(summaries.Count);

        foreach (var summary in summaries)
        {
            var dayStart = calendar.DayStartUnix(summary.Date);
            var dayEnd = calendar.DayEndUnix(summary.Date);
            var timeline = byDate.TryGetValue(summary.Date, out var day) ? day.EffectiveTimeline() : [];

            var otherCarrier = timeline
                .Where(e => e.Source == DutyEventSource.OtherCarrier)
                .GroupBy(e => e.CarrierName ?? string.Empty)
                .Select(g => new HosAuditOtherCarrierRow(g.Key, g.Count()))
                .OrderBy(r => r.CarrierName, StringComparer.Ordinal)
                .ToList();

            var outOfService = records.OutOfService
                .Where(o => o.Overlaps(dayStart, dayEnd))
                .OrderBy(o => o.DeclaredAtUtc)
                .FirstOrDefault();

            rows.Add(new HosAuditDayResponse(
                summary.DriverId,
                summary.Date,
                summary.HasRecords,
                timeline.Count,
                timeline.Select(e => HosDisplay.SourceToWire(e.Source)).Distinct().ToList(),
                summary.Certified,
                summary.CertifiedAtUtc,
                summary.RecertificationRequired,
                summary.Totals,
                summary.PersonalConveyanceKm,
                summary.Violations,
                records.Exceptions
                    .Where(e => e.Overlaps(dayStart, dayEnd))
                    .OrderBy(e => e.EffectiveFromUtc)
                    .Select(ComplianceResponseMapper.ToResponse)
                    .ToList(),
                outOfService is null ? null : ComplianceResponseMapper.ToResponse(outOfService, now),
                records.Malfunctions.Any(m => m.Overlaps(dayStart, dayEnd)),
                otherCarrier,
                DutyLedgerAssembler.IsAttestedDay(summary.Date, records.PriorRecordsCoveredThroughDate),
                records.Findings
                    .Where(f => f.LocalDate == summary.Date)
                    .OrderBy(f => f.OpenedAtUtc)
                    .Select(ComplianceResponseMapper.ToResponse)
                    .ToList(),
                HosDisplay.PlanningLabel));
        }

        return rows;
    }
}
