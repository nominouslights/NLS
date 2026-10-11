using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Ledger;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.DutyDays.GetOne;

public sealed class GetDutyDayQueryHandler(IDutyDayRepository repository, TimeProvider clock)
    : IQueryHandler<GetDutyDayQuery, DutyDaySummaryResponse>
{
    public async Task<Result<DutyDaySummaryResponse>> Handle(GetDutyDayQuery query, CancellationToken cancellationToken)
    {
        if (!await repository.DriverExistsAsync(query.DriverId, cancellationToken))
        {
            return Result.Failure<DutyDaySummaryResponse>(DriverErrors.NotFound);
        }

        var nowUnix = clock.GetUtcNow().ToUnixTimeSeconds();
        if (query.LocalDate > DutyLedgerAssembler.Calendar.DayOf(nowUnix))
        {
            return Result.Failure<DutyDaySummaryResponse>(HosErrors.DayNotFound);
        }

        var days = await repository.LoadRangeAsync(
            query.DriverId, query.LocalDate.AddDays(-DutyLedgerAssembler.LookbackDays), query.LocalDate, cancellationToken);
        var knownFrom = await repository.EarliestLocalDateAsync(query.DriverId, cancellationToken);

        var summaries = DutyDaySummaryBuilder.Build(query.DriverId, days, knownFrom, query.LocalDate, query.LocalDate, nowUnix);
        return Result.Success(summaries[0]);
    }
}
