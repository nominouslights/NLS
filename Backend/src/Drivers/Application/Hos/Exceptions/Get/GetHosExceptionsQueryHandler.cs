using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Application.Hos.DutyDays.GetRange;
using NorthernLink.Drivers.Application.Hos.Ledger;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Exceptions.Get;

public sealed class GetHosExceptionsQueryHandler(IComplianceRecordsReader compliance)
    : IQueryHandler<GetHosExceptionsQuery, IReadOnlyList<HosExceptionResponse>>
{
    public async Task<Result<IReadOnlyList<HosExceptionResponse>>> Handle(GetHosExceptionsQuery query, CancellationToken cancellationToken)
    {
        if (query.From > query.To || query.To.DayNumber - query.From.DayNumber >= GetDutyDaysQueryHandler.MaxRangeDays)
        {
            return Result.Failure<IReadOnlyList<HosExceptionResponse>>(GetDutyDaysQueryHandler.RangeInvalid);
        }

        var calendar = DutyLedgerAssembler.Calendar;
        var from = calendar.DayStartUnix(query.From);
        var to = calendar.DayEndUnix(query.To);

        var records = await compliance.LoadAsync(query.DriverId, DateTimeOffset.FromUnixTimeSeconds(from), cancellationToken);
        if (records is null)
        {
            return Result.Failure<IReadOnlyList<HosExceptionResponse>>(DriverErrors.NotFound);
        }

        return Result.Success<IReadOnlyList<HosExceptionResponse>>(records.Exceptions
            .Where(e => e.Overlaps(from, to))
            .OrderBy(e => e.EffectiveFromUtc)
            .Select(ComplianceResponseMapper.ToResponse)
            .ToList());
    }
}
