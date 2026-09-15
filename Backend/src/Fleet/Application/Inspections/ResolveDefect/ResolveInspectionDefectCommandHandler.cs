using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Application.Inspections.ResolveDefect;

/// <summary>
/// Loads the inspection tenant-filtered (a cross-tenant id resolves to null and yields
/// <see cref="InspectionErrors.NotFound"/>), then lets the aggregate do the addressing and the
/// already-resolved check. The resolution instant is stamped here, not taken from the client.
/// </summary>
public sealed class ResolveInspectionDefectCommandHandler(IVehicleInspectionRepository repository)
    : ICommandHandler<ResolveInspectionDefectCommand>
{
    public async Task<Result> Handle(ResolveInspectionDefectCommand command, CancellationToken cancellationToken)
    {
        var inspection = await repository.GetByIdAsync(command.InspectionId, cancellationToken);
        if (inspection is null)
        {
            return Result.Failure(InspectionErrors.NotFound);
        }

        // Same fallback as EnterInspection's EnteredBy: a dispatcher-entered action with no name
        // on it is attributed to "Dispatch" rather than left blank.
        var resolvedBy = string.IsNullOrWhiteSpace(command.ResolvedBy) ? "Dispatch" : command.ResolvedBy;

        var result = inspection.ResolveDefect(
            command.Item,
            command.Reason,
            command.Note,
            resolvedBy,
            DateTimeOffset.UtcNow);

        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
