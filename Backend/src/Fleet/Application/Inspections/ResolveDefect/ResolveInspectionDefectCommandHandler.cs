using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Application.Inspections.ResolveDefect;

/// <summary>
/// Loads the inspection tenant-filtered (a cross-tenant id resolves to null and yields
/// <see cref="InspectionErrors.NotFound"/>), then lets the aggregate do the addressing and the
/// already-resolved check. The resolution instant is stamped here, not taken from the client.
///
/// Once the addressed defect is resolved, the same component's open duplicates on the vehicle's
/// other inspections are cleared with the same stamp (<see cref="DuplicateDefectCascade"/>).
/// Duplicates never make the call fail: every failure mode is the addressed defect's own. All
/// touched inspections share this request's DbContext, so one save commits them together.
/// </summary>
public sealed class ResolveInspectionDefectCommandHandler(
    IVehicleInspectionRepository repository,
    IVehicleRepository vehicles)
    : ICommandHandler<ResolveInspectionDefectCommand, ResolveInspectionDefectResponse>
{
    public async Task<Result<ResolveInspectionDefectResponse>> Handle(
        ResolveInspectionDefectCommand command,
        CancellationToken cancellationToken)
    {
        var inspection = await repository.GetByIdAsync(command.InspectionId, cancellationToken);
        if (inspection is null)
        {
            return Result.Failure<ResolveInspectionDefectResponse>(InspectionErrors.NotFound);
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
            return Result.Failure<ResolveInspectionDefectResponse>(result.Error);
        }

        var addressed = inspection.FindDefect(command.Item)!;

        var duplicates = await DuplicateDefectCascade.ResolveAsync(
            repository, vehicles, inspection, addressed.Item, cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success(new ResolveInspectionDefectResponse(
        [
            ResolvedDefectResponse.From(inspection, addressed),
            .. duplicates.Select(d => ResolvedDefectResponse.From(d.Inspection, d.Defect)),
        ]));
    }
}
