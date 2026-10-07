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
/// inspection, no defects) is still created with no defect lines.
///
/// Whether a defect is free is decided by <see cref="VehicleInspection.CanAttachDefect"/> alone,
/// for both request forms. That includes the legacy hold: while an inspection's
/// <c>GeneratedWorkOrderId</c> names a work order that is still open (loaded here — it is another
/// aggregate), its unresolved, unattached defects belong to that work order, so naming one fails
/// with <see cref="InspectionErrors.DefectAlreadyOnWorkOrder"/> and the whole-inspection form
/// leaves them out (an inspection held entirely that way fails with
/// <see cref="InspectionErrors.NoOpenDefectsToAttach"/>).
///
/// New code never calls <see cref="VehicleInspection.LinkWorkOrder"/>:
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

        var inspections = new Dictionary<Guid, (VehicleInspection Inspection, bool GeneratedWorkOrderIsOpen)>();
        string? vehicleUnit = null;

        async Task<Result<(VehicleInspection Inspection, bool GeneratedWorkOrderIsOpen)>> LoadAsync(Guid inspectionId)
        {
            if (inspections.TryGetValue(inspectionId, out var cached))
            {
                return Result.Success(cached);
            }

            var inspection = await inspectionRepository.GetByIdAsync(inspectionId, cancellationToken);
            if (inspection is null)
            {
                return Result.Failure<(VehicleInspection, bool)>(InspectionErrors.NotFound);
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
                return Result.Failure<(VehicleInspection, bool)>(InspectionErrors.VehicleMismatch);
            }

            // A legacy whole-inspection work order (before per-defect links) still holds this
            // inspection's unattached defects while it is open — its completion resolves them.
            // A generated id naming no work order holds nothing.
            var generatedWorkOrderIsOpen = false;
            if (inspection.GeneratedWorkOrderId is { } generatedWorkOrderId)
            {
                var generated = await repository.GetByIdAsync(generatedWorkOrderId, cancellationToken);
                generatedWorkOrderIsOpen = generated is { IsTerminal: false };
            }

            var entry = (inspection, generatedWorkOrderIsOpen);
            inspections[inspectionId] = entry;
            return Result.Success(entry);
        }

        var requested = new List<(VehicleInspection Inspection, bool GeneratedWorkOrderIsOpen, InspectionDefect Defect)>();

        foreach (var reference in command.Defects ?? [])
        {
            var loaded = await LoadAsync(reference.InspectionId);
            if (loaded.IsFailure)
            {
                return Result.Failure<Guid>(loaded.Error);
            }

            var (inspection, generatedWorkOrderIsOpen) = loaded.Value;
            var attachable = inspection.CanAttachDefect(reference.Item, generatedWorkOrderIsOpen);
            if (attachable.IsFailure)
            {
                return Result.Failure<Guid>(attachable.Error);
            }

            requested.Add((inspection, generatedWorkOrderIsOpen, inspection.FindDefect(reference.Item)!));
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

            var (inspection, generatedWorkOrderIsOpen) = loaded.Value;
            foreach (var defect in inspection.AttachableDefects(generatedWorkOrderIsOpen))
            {
                if (!requested.Any(r => r.Inspection.Id == wholeInspectionId && ReferenceEquals(r.Defect, defect)))
                {
                    requested.Add((inspection, generatedWorkOrderIsOpen, defect));
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
        foreach (var (inspection, generatedWorkOrderIsOpen, defect) in requested)
        {
            var assigned = inspection.AssignDefectToWorkOrder(defect.Item, workOrder.Id, generatedWorkOrderIsOpen);
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
