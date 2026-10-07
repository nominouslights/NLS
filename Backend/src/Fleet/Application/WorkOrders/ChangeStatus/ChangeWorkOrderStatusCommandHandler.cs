using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.WorkOrders;

namespace NorthernLink.Fleet.Application.WorkOrders.ChangeStatus;

/// <summary>
/// Advances a work order's status. Cancelling also releases every defect still attached to it,
/// so each can go on a new work order; the defects stay open (a cancelled repair fixed nothing)
/// and the work order keeps its lines as the record of what it was raised for. One save.
/// </summary>
public sealed class ChangeWorkOrderStatusCommandHandler(
    IWorkOrderRepository repository,
    IVehicleInspectionRepository inspectionRepository)
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

        if (workOrder.Status == WorkOrderStatus.Cancelled)
        {
            foreach (var group in workOrder.Defects.GroupBy(l => l.InspectionId))
            {
                // An inspection removed since is simply skipped — nothing left to release.
                var inspection = await inspectionRepository.GetByIdAsync(group.Key, cancellationToken);
                foreach (var line in group)
                {
                    inspection?.ReleaseDefectFromWorkOrder(line.Item, workOrder.Id);
                }
            }
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
