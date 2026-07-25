using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.WorkOrders;

namespace NorthernLink.Fleet.Application.WorkOrders.ChangeStatus;

public sealed class ChangeWorkOrderStatusCommandHandler(IWorkOrderRepository repository)
    : ICommandHandler<ChangeWorkOrderStatusCommand>
{
    public async Task<Result> Handle(ChangeWorkOrderStatusCommand command, CancellationToken cancellationToken)
    {
        var workOrder = await repository.GetByIdAsync(command.WorkOrderId, cancellationToken);
        if (workOrder is null)
        {
            return Result.Failure(WorkOrderErrors.NotFound);
        }

        var result = workOrder.ChangeStatus(command.Status);
        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
