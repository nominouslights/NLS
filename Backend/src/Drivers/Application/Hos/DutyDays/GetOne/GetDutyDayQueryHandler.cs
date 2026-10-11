using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Ledger;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.DutyDays.GetOne;

public sealed class GetDutyDayQueryHandler(
    IDutyDayRepository repository,
    IComplianceRecordsReader compliance,
    TimeProvider clock)
    : IQueryHandler<GetDutyDayQuery, DutyDaySummaryResponse>
{
    public async Task<Result<DutyDaySummaryResponse>> Handle(GetDutyDayQuery query, CancellationToken cancellationToken)
    {
        var nowUnix = clock.GetUtcNow().ToUnixTimeSeconds();
        var loadFrom = query.LocalDate.AddDays(-DutyLedgerAssembler.LookbackDays);

        var records = await compliance.LoadAsync(
            query.DriverId, DateTimeOffset.FromUnixTimeSeconds(DutyLedgerAssembler.Calendar.DayStartUnix(loadFrom)), cancellationToken);
        if (records is null)
        {
            return Result.Failure<DutyDaySummaryResponse>(DriverErrors.NotFound);
        }

        if (query.LocalDate > DutyLedgerAssembler.Calendar.DayOf(nowUnix))
        {
            return Result.Failure<DutyDaySummaryResponse>(HosErrors.DayNotFound);
        }

        var days = await repository.LoadRangeAsync(query.DriverId, loadFrom, query.LocalDate, cancellationToken);
        var knownFrom = await repository.EarliestLocalDateAsync(query.DriverId, cancellationToken);

        var summaries = DutyDaySummaryBuilder.Build(query.DriverId, days, knownFrom, query.LocalDate, query.LocalDate, nowUnix, records);
        return Result.Success(summaries[0]);
    }
}
