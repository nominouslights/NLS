using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Ledger;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.DutyDays.GetRange;

public sealed class GetDutyDaysQueryHandler(IDutyDayRepository repository, TimeProvider clock)
    : IQueryHandler<GetDutyDaysQuery, IReadOnlyList<DutyDaySummaryResponse>>
{
    public const int MaxRangeDays = 92;

    public static readonly Error RangeInvalid = Error.Validation(
        "Drivers.Hos.RangeInvalid", $"from must not be after to, and the range may span at most {MaxRangeDays} days.");

    public async Task<Result<IReadOnlyList<DutyDaySummaryResponse>>> Handle(GetDutyDaysQuery query, CancellationToken cancellationToken)
    {
        if (query.From > query.To || query.To.DayNumber - query.From.DayNumber >= MaxRangeDays)
        {
            return Result.Failure<IReadOnlyList<DutyDaySummaryResponse>>(RangeInvalid);
        }

        if (!await repository.DriverExistsAsync(query.DriverId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<DutyDaySummaryResponse>>(DriverErrors.NotFound);
        }

        var nowUnix = clock.GetUtcNow().ToUnixTimeSeconds();
        var today = DutyLedgerAssembler.Calendar.DayOf(nowUnix);
        var to = query.To > today ? today : query.To;
        if (query.From > to)
        {
            return Result.Success<IReadOnlyList<DutyDaySummaryResponse>>([]);
        }

        var days = await repository.LoadRangeAsync(
            query.DriverId, query.From.AddDays(-DutyLedgerAssembler.LookbackDays), to, cancellationToken);
        var knownFrom = await repository.EarliestLocalDateAsync(query.DriverId, cancellationToken);

        return Result.Success(DutyDaySummaryBuilder.Build(query.DriverId, days, knownFrom, query.From, to, nowUnix));
    }
}
