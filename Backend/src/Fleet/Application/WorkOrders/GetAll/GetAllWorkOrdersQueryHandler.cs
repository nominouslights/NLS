using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.WorkOrders;

namespace NorthernLink.Fleet.Application.WorkOrders.GetAll;

public sealed class GetAllWorkOrdersQueryHandler(IWorkOrderReadService readService)
    : IQueryHandler<GetAllWorkOrdersQuery, IReadOnlyList<WorkOrderResponse>>
{
    public async Task<Result<IReadOnlyList<WorkOrderResponse>>> Handle(
        GetAllWorkOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var workOrders = await readService.GetAllAsync(cancellationToken);
        return Result.Success(workOrders);
    }
}
