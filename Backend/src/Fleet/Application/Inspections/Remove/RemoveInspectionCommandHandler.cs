using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Application.Inspections.Remove;

/// <summary>
/// Hard-deletes an inspection. Loads it tenant-filtered (a cross-tenant id resolves to null and
/// yields <see cref="InspectionErrors.NotFound"/>), raises the removal domain event on the
/// aggregate (so the mapper emits the public integration event), then deletes the row. The audit
/// pipeline still writes a final snapshot and the synthetic aggregate-deleted journal row, which
/// is what drives the read model to drop its copy.
/// </summary>
public sealed class RemoveInspectionCommandHandler(IVehicleInspectionRepository repository)
    : ICommandHandler<RemoveInspectionCommand>
{
    public async Task<Result> Handle(RemoveInspectionCommand command, CancellationToken cancellationToken)
    {
        var inspection = await repository.GetByIdAsync(command.InspectionId, cancellationToken);
        if (inspection is null)
        {
            return Result.Failure(InspectionErrors.NotFound);
        }

        inspection.MarkRemoved();
        repository.Remove(inspection);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
