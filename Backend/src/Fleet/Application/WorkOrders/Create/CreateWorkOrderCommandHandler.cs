using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Inspections;
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

        // Resolve the generating inspection up front so an unknown id fails before anything
        // is created rather than being silently skipped.
        VehicleInspection? inspection = null;
        if (command.InspectionId is { } inspectionId)
        {
            inspection = await inspectionRepository.GetByIdAsync(inspectionId, cancellationToken);

            if (inspection is null)
            {
                return Result.Failure<Guid>(InspectionErrors.NotFound);
            }
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

        // Link the generating inspection (same DbContext → one SaveChanges keeps create+link
        // atomic). Already-linked always fails — even if the earlier work order was cancelled;
        // the cancelled-WO escape hatch is a deliberate follow-up.
        if (inspection is not null)
        {
            var linkResult = inspection.LinkWorkOrder(workOrder.Id);

            if (linkResult.IsFailure)
            {
                return Result.Failure<Guid>(linkResult.Error);
            }
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(workOrder.Id);
    }
}
