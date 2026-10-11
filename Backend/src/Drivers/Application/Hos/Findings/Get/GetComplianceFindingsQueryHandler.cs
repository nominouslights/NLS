using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Application.Hos.Ledger;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Findings.Get;

public sealed class GetComplianceFindingsQueryHandler(IComplianceRecordsReader compliance)
    : IQueryHandler<GetComplianceFindingsQuery, IReadOnlyList<ComplianceFindingResponse>>
{
    public async Task<Result<IReadOnlyList<ComplianceFindingResponse>>> Handle(GetComplianceFindingsQuery query, CancellationToken cancellationToken)
    {
        var fromUtc = query.From is { } from
            ? DateTimeOffset.FromUnixTimeSeconds(DutyLedgerAssembler.Calendar.DayStartUnix(from))
            : DateTimeOffset.MinValue;

        var records = await compliance.LoadAsync(query.DriverId, fromUtc, cancellationToken);
        if (records is null)
        {
            return Result.Failure<IReadOnlyList<ComplianceFindingResponse>>(DriverErrors.NotFound);
        }

        return Result.Success<IReadOnlyList<ComplianceFindingResponse>>(records.Findings
            .Where(f => (query.From is not { } a || f.LocalDate >= a) && (query.To is not { } b || f.LocalDate <= b))
            .OrderByDescending(f => f.LocalDate)
            .ThenByDescending(f => f.OpenedAtUtc)
            .Select(ComplianceResponseMapper.ToResponse)
            .ToList());
    }
}
