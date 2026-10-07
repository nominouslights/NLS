using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Domain.WorkOrders;

namespace NorthernLink.Fleet.Application.WorkOrders.Create;

/// <summary>
/// Creates the work order and attaches each named defect to it — all on one scoped DbContext,
/// one SaveChanges, so the work order and every defect link commit together or not at all.
///
/// Every check runs BEFORE anything is mutated or added: an unknown inspection, a defect on
/// another vehicle, a missing/resolved/already-attached defect each fail the whole request
/// with nothing changed. A request naming an inspection that leaves nothing to attach fails
/// with <see cref="InspectionErrors.NoOpenDefectsToAttach"/>; a plain manual work order (no
/// inspection, no defects) is still created with no defect lines. New code never calls <see cref="VehicleInspection.LinkWorkOrder"/>:
/// <c>GeneratedWorkOrderId</c> stays on the inspection as history for work orders created
/// before per-defect links.
/// </summary>
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

        var inspections = new Dictionary<Guid, VehicleInspection>();
        string? vehicleUnit = null;

        async Task<Result<VehicleInspection>> LoadAsync(Guid inspectionId)
        {
            if (inspections.TryGetValue(inspectionId, out var cached))
            {
                return Result.Success(cached);
            }

            var inspection = await inspectionRepository.GetByIdAsync(inspectionId, cancellationToken);
            if (inspection is null)
            {
                return Result.Failure<VehicleInspection>(InspectionErrors.NotFound);
            }

            // Same vehicle only. A legacy unit-only inspection (no vehicle link) matches on the
            // unit number — exactly the rule the defects panel uses to list it under this truck.
            var belongs = inspection.VehicleId is { } inspectionVehicleId
                ? inspectionVehicleId == command.VehicleId
                : string.Equals(
                    inspection.Unit.Trim(),
                    (vehicleUnit ??= await repository.FindVehicleUnitNumberAsync(command.VehicleId, cancellationToken))?.Trim(),
                    StringComparison.Ordinal);

            if (!belongs)
            {
                return Result.Failure<VehicleInspection>(InspectionErrors.VehicleMismatch);
            }

            inspections[inspectionId] = inspection;
            return Result.Success(inspection);
        }

        var requested = new List<(VehicleInspection Inspection, InspectionDefect Defect)>();

        foreach (var reference in command.Defects ?? [])
        {
            var loaded = await LoadAsync(reference.InspectionId);
            if (loaded.IsFailure)
            {
                return Result.Failure<Guid>(loaded.Error);
            }

            var defect = loaded.Value.FindDefect(reference.Item);
            if (defect is null)
            {
                return Result.Failure<Guid>(InspectionErrors.DefectNotFound);
            }

            if (defect.IsResolved)
            {
                return Result.Failure<Guid>(InspectionErrors.DefectAlreadyResolved);
            }

            if (defect.WorkOrderId is not null)
            {
                return Result.Failure<Guid>(InspectionErrors.DefectAlreadyOnWorkOrder);
            }

            requested.Add((loaded.Value, defect));
        }

        // The legacy whole-inspection form: every open, unattached defect, through the same path.
        // A defect already named explicitly is not added twice.
        if (command.InspectionId is { } wholeInspectionId)
        {
            var loaded = await LoadAsync(wholeInspectionId);
            if (loaded.IsFailure)
            {
                return Result.Failure<Guid>(loaded.Error);
            }

            foreach (var defect in loaded.Value.Defects.Where(d => !d.IsResolved && d.WorkOrderId is null))
            {
                if (!requested.Any(r => r.Inspection.Id == wholeInspectionId && ReferenceEquals(r.Defect, defect)))
                {
                    requested.Add((loaded.Value, defect));
                }
            }

            // Naming an inspection is a request to put its defects on a work order. With nothing
            // left to attach (a double-submit, or everything resolved since), refuse rather than
            // create an empty, unlinked inspection-sourced work order that resolves nothing.
            if (requested.Count == 0)
            {
                return Result.Failure<Guid>(InspectionErrors.NoOpenDefectsToAttach);
            }
        }

        var lines = requested
            .Select(r => new WorkOrderDefectLine
            {
                InspectionId = r.Inspection.Id,
                Item = r.Defect.Item,
                Severity = r.Defect.Severity,
                Note = r.Defect.Note,
            })
            .ToList();

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
            command.DateRequiredOrOos,
            lines);

        if (workOrderResult.IsFailure)
        {
            return Result.Failure<Guid>(workOrderResult.Error);
        }

        var workOrder = workOrderResult.Value;

        // Pre-validated above and duplicates rejected by Create, so these cannot fail in
        // practice — but the aggregate stays the authority, and nothing has been saved yet.
        foreach (var (inspection, defect) in requested)
        {
            var assigned = inspection.AssignDefectToWorkOrder(defect.Item, workOrder.Id);
            if (assigned.IsFailure)
            {
                return Result.Failure<Guid>(assigned.Error);
            }
        }

        repository.Add(workOrder);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(workOrder.Id);
    }
}
