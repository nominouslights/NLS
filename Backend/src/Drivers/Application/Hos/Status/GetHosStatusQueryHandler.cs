using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Ledger;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.HoursOfService;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Status;

public sealed class GetHosStatusQueryHandler(IDutyDayRepository repository, TimeProvider clock)
    : IQueryHandler<GetHosStatusQuery, HosStatusResponse>
{
    public async Task<Result<HosStatusResponse>> Handle(GetHosStatusQuery query, CancellationToken cancellationToken)
    {
        if (!await repository.DriverExistsAsync(query.DriverId, cancellationToken))
        {
            return Result.Failure<HosStatusResponse>(DriverErrors.NotFound);
        }

        var nowUnix = clock.GetUtcNow().ToUnixTimeSeconds();
        var today = DutyLedgerAssembler.Calendar.DayOf(nowUnix);
        var days = await repository.LoadRangeAsync(
            query.DriverId, today.AddDays(-DutyLedgerAssembler.LookbackDays), today, cancellationToken);
        var knownFrom = await repository.EarliestLocalDateAsync(query.DriverId, cancellationToken);

        var ledger = DutyLedgerAssembler.Build(query.DriverId, days, knownFrom, nowUnix);
        var evaluation = DutyLedgerAssembler.Evaluate(ledger, nowUnix);

        return Result.Success(Assemble(query.DriverId, evaluation, knownFrom));
    }

    public static HosStatusResponse Assemble(Guid driverId, HosEvaluation evaluation, DateOnly? knownFromDate) => new(
        driverId,
        HosDisplay.PlanningLabel,
        HosWire.Status(evaluation.CurrentStatus),
        DateTimeOffset.FromUnixTimeSeconds(evaluation.CurrentStatusSinceUnix),
        RecordsOnFile: !evaluation.BlockingReasons.Contains(BlockingReason.NoLedger)
            && !evaluation.BlockingReasons.Contains(BlockingReason.RecordsIncomplete),
        knownFromDate is { } first
            ? DateTimeOffset.FromUnixTimeSeconds(DutyLedgerAssembler.Calendar.DayStartUnix(first))
            : null,
        OutOfService: null,
        EldMalfunctionOpen: false,
        evaluation);
}
