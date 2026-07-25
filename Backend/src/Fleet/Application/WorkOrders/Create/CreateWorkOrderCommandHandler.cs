using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Domain.WorkOrders;

namespace NorthernLink.Fleet.Application.WorkOrders.Create;

public sealed class CreateWorkOrderCommandHandler(
    IWorkOrderRepository repository,
    IVehicleInspectionRepository inspectionRepository)
    : ICommandHandler<CreateWorkOrderCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateWorkOrderCommand command, CancellationToken cancellationToken)
    {
        if (!await repository.VehicleExistsAsync(command.VehicleId, cancellationToken))
        {
            return Result.Failure<Guid>(VehicleErrors.NotFound);
        }

        var sequence = await repository.NextSequenceAsync(command.TenantId, cancellationToken);
        var number = $"WO-{sequence}";

        var workOrderResult = WorkOrder.Create(
            command.TenantId,
            command.VehicleId,
            number,
            command.Title,
            command.Description,
            command.Priority,
            command.Source,
            command.SourceRef,
            "Dispatch",
            command.AssignedTo,
            command.DueDate,
            command.LineItems,
            command.ShopId,
            command.AuthorizedLimitCad,
            command.BudgetCode,
            command.DateRequiredOrOos);

        if (workOrderResult.IsFailure)
        {
            return Result.Failure<Guid>(workOrderResult.Error);
        }

        var workOrder = workOrderResult.Value;
        repository.Add(workOrder);

        // Link the generating inspection (same DbContext → one transaction).
        if (command.InspectionId is { } inspectionId)
        {
            var inspection = await inspectionRepository.GetByIdAsync(inspectionId, cancellationToken);
            inspection?.LinkWorkOrder(workOrder.Id);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(workOrder.Id);
    }
}
