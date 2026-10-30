using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.OutOfService.Get;

public sealed class GetOutOfServiceRecordsQueryHandler(IComplianceRecordsReader compliance, TimeProvider clock)
    : IQueryHandler<GetOutOfServiceRecordsQuery, IReadOnlyList<OutOfServiceRecordResponse>>
{
    public async Task<Result<IReadOnlyList<OutOfServiceRecordResponse>>> Handle(GetOutOfServiceRecordsQuery query, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var records = await compliance.LoadAsync(query.DriverId, DateTimeOffset.MinValue, cancellationToken);
        if (records is null)
        {
            return Result.Failure<IReadOnlyList<OutOfServiceRecordResponse>>(DriverErrors.NotFound);
        }

        return Result.Success<IReadOnlyList<OutOfServiceRecordResponse>>(records.OutOfService
            .OrderByDescending(r => r.DeclaredAtUtc)
            .Select(r => ComplianceResponseMapper.ToResponse(r, now))
            .ToList());
    }
}
