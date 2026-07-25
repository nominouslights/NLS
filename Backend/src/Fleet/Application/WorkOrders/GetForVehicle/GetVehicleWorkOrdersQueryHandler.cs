using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.WorkOrders;

namespace NorthernLink.Fleet.Application.WorkOrders.GetForVehicle;

public sealed class GetVehicleWorkOrdersQueryHandler(IWorkOrderReadService readService)
    : IQueryHandler<GetVehicleWorkOrdersQuery, IReadOnlyList<WorkOrderResponse>>
{
    public async Task<Result<IReadOnlyList<WorkOrderResponse>>> Handle(
        GetVehicleWorkOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var workOrders = await readService.GetForVehicleAsync(query.VehicleId, cancellationToken);
        return Result.Success(workOrders);
    }
}
