using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Services;

namespace NorthernLink.Fleet.Application.Services.GetForVehicle;

public sealed class GetVehicleServiceRecordsQueryHandler(IServiceRecordReadService readService)
    : IQueryHandler<GetVehicleServiceRecordsQuery, IReadOnlyList<ServiceRecordResponse>>
{
    public async Task<Result<IReadOnlyList<ServiceRecordResponse>>> Handle(
        GetVehicleServiceRecordsQuery query,
        CancellationToken cancellationToken)
    {
        var records = await readService.GetForVehicleAsync(query.VehicleId, cancellationToken);
        return Result.Success(records);
    }
}
