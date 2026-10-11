using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Application.Hos.DutyDays;
using NorthernLink.Drivers.Application.Hos.Ledger;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Shared.HoursOfService;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Status;

public sealed class GetHosStatusQueryHandler(
    IDutyDayRepository repository,
    IComplianceRecordsReader compliance,
    TimeProvider clock)
    : IQueryHandler<GetHosStatusQuery, HosStatusResponse>
{
    public async Task<Result<HosStatusResponse>> Handle(GetHosStatusQuery query, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var nowUnix = now.ToUnixTimeSeconds();
        var today = DutyLedgerAssembler.Calendar.DayOf(nowUnix);
        var windowFrom = today.AddDays(-DutyLedgerAssembler.LookbackDays);

        var records = await compliance.LoadAsync(
            query.DriverId, DateTimeOffset.FromUnixTimeSeconds(DutyLedgerAssembler.Calendar.DayStartUnix(windowFrom)), cancellationToken);
        if (records is null)
        {
            return Result.Failure<HosStatusResponse>(DriverErrors.NotFound);
        }

        var days = await repository.LoadRangeAsync(query.DriverId, windowFrom, today, cancellationToken);
        var earliest = await repository.EarliestLocalDateAsync(query.DriverId, cancellationToken);

        var ledger = DutyLedgerAssembler.Build(query.DriverId, days, earliest, nowUnix, records);
        var evaluation = DutyLedgerAssembler.Evaluate(ledger, nowUnix);

        return Result.Success(Assemble(query.DriverId, evaluation, earliest, records, now));
    }

    public static HosStatusResponse Assemble(
        Guid driverId,
        HosEvaluation evaluation,
        DateOnly? earliestDay,
        ComplianceRecords records,
        DateTimeOffset now)
    {
        var knownFrom = DutyLedgerAssembler.KnownFromDate(earliestDay, records.PriorRecordsCoveredThroughDate);
        var outOfService = records.OutOfService
            .Where(o => o.IsActiveAt(now))
            .OrderBy(o => o.DeclaredAtUtc)
            .FirstOrDefault();

        return new HosStatusResponse(
            driverId,
            HosDisplay.PlanningLabel,
            HosWire.Status(evaluation.CurrentStatus),
            DateTimeOffset.FromUnixTimeSeconds(evaluation.CurrentStatusSinceUnix),
            RecordsOnFile: !evaluation.BlockingReasons.Contains(BlockingReason.NoLedger)
                && !evaluation.BlockingReasons.Contains(BlockingReason.RecordsIncomplete),
            knownFrom is { } first
                ? DateTimeOffset.FromUnixTimeSeconds(DutyLedgerAssembler.Calendar.DayStartUnix(first))
                : null,
            outOfService is null ? null : DutyDaySummaryBuilder.ToResponse(outOfService),
            records.Malfunctions.Any(m => m.IsOpen),
            PriorRecords(records),
            evaluation);
    }

    public static PriorRecordsResponse? PriorRecords(ComplianceRecords records) =>
        records.PriorRecordsCoveredThroughDate is { } through
            ? new PriorRecordsResponse(
                through,
                records.PriorRecordsAttestedAtUtc ?? DateTimeOffset.MinValue,
                records.PriorRecordsAttestedByUserId ?? Guid.Empty,
                records.PriorRecordsNote,
                records.WorksForOtherCarriers)
            : null;
}
