using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.EldMalfunctions.Get;

public sealed class GetEldMalfunctionsQueryHandler(IComplianceRecordsReader compliance)
    : IQueryHandler<GetEldMalfunctionsQuery, IReadOnlyList<EldMalfunctionResponse>>
{
    public async Task<Result<IReadOnlyList<EldMalfunctionResponse>>> Handle(GetEldMalfunctionsQuery query, CancellationToken cancellationToken)
    {
        var records = await compliance.LoadAsync(query.DriverId, DateTimeOffset.MinValue, cancellationToken);
        if (records is null)
        {
            return Result.Failure<IReadOnlyList<EldMalfunctionResponse>>(DriverErrors.NotFound);
        }

        return Result.Success<IReadOnlyList<EldMalfunctionResponse>>(records.Malfunctions
            .OrderByDescending(m => m.IsOpen)
            .ThenByDescending(m => m.NoticedAtUtc)
            .Select(ComplianceResponseMapper.ToResponse)
            .ToList());
    }
}
