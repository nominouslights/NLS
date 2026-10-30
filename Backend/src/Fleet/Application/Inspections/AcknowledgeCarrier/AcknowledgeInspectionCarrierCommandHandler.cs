using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Application.Inspections.AcknowledgeCarrier;

/// <summary>
/// Loads the inspection tenant-filtered (a cross-tenant id resolves to null and yields
/// <see cref="InspectionErrors.NotFound"/>), then lets the aggregate do the result check, the
/// already-acknowledged check and the blank-name check. The acknowledgement instant is stamped
/// here, not taken from the client — the same rule as the defect-resolution path.
/// </summary>
public sealed class AcknowledgeInspectionCarrierCommandHandler(IVehicleInspectionRepository repository)
    : ICommandHandler<AcknowledgeInspectionCarrierCommand>
{
    public async Task<Result> Handle(AcknowledgeInspectionCarrierCommand command, CancellationToken cancellationToken)
    {
        var inspection = await repository.GetByIdAsync(command.InspectionId, cancellationToken);
        if (inspection is null)
        {
            return Result.Failure(InspectionErrors.NotFound);
        }

        // No "Dispatch" fallback here, deliberately: EnteredBy and ResolvedBy default a missing
        // name because an unattributed action is still an action, but a carrier acknowledgement
        // IS the name. An empty one goes to the aggregate and comes back rejected.
        var result = inspection.AcknowledgeAsCarrier(
            command.AcknowledgedBy ?? string.Empty,
            command.Note,
            DateTimeOffset.UtcNow);

        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
