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
/// Two phases. Phase 1 resolves the addressed defect and saves it, exactly as before this
/// endpoint cleared duplicates — every failure mode of the call is phase 1's. Phase 2 runs only
/// for a repair with a non-empty <see cref="ResolveInspectionDefectCommand.ClearDuplicates"/>:
/// it clears the listed reports that are still valid duplicates (reported before this resolution,
/// same vehicle, same component, still open), in a separate save that retries version conflicts
/// and otherwise leaves them open — it can never fail the call (<see cref="DuplicateDefectCascade"/>).
/// </summary>
public sealed class ResolveInspectionDefectCommandHandler(
    IVehicleInspectionRepository repository,
    DuplicateDefectCascade duplicates)
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

        var requested = command.ClearDuplicates ?? [];
        if (requested.Count > 0 && !command.Reason.ClearsDuplicates())
        {
            return Result.Failure<ResolveInspectionDefectResponse>(InspectionErrors.DuplicatesOnlyClearedByRepair);
        }

        // Same fallback as EnterInspection's EnteredBy: a dispatcher-entered action with no name
        // on it is attributed to "Dispatch" rather than left blank.
        var resolvedBy = string.IsNullOrWhiteSpace(command.ResolvedBy) ? "Dispatch" : command.ResolvedBy;
        var now = DateTimeOffset.UtcNow;

        // ---- Phase 1: the addressed defect, saved on its own. ----
        var result = inspection.ResolveDefect(command.Item, command.Reason, command.Note, resolvedBy, now);
        if (result.IsFailure)
        {
            return Result.Failure<ResolveInspectionDefectResponse>(result.Error);
        }

        var addressed = inspection.FindDefect(command.Item)!;
        await repository.SaveChangesAsync(cancellationToken);

        var response = new List<ResolvedDefectResponse> { ResolvedDefectResponse.From(inspection, addressed) };

        // ---- Phase 2: only what the dispatcher was shown, and only what still qualifies. ----
        if (requested.Count > 0)
        {
            var cleared = await duplicates.ClearForSourceAsync(
                new DefectOnInspection(inspection, addressed),
                DefectRepairCutoff.ForManualRepair(now),
                candidate => requested.Any(r =>
                    r.InspectionId == candidate.Inspection.Id
                    && VehicleInspection.IsSameItem(r.Item, candidate.Defect.Item)),
                cancellationToken);

            response.AddRange(cleared.Select(d => ResolvedDefectResponse.From(d.Inspection, d.Defect)));
        }

        return Result.Success(new ResolveInspectionDefectResponse(response));
    }
}
