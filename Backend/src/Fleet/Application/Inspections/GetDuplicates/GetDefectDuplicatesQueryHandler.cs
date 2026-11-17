using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Application.Inspections.GetDuplicates;

/// <summary>
/// Addresses the defect the way the resolve does — tenant-filtered load
/// (<see cref="InspectionErrors.NotFound"/>), then the item (<see cref="InspectionErrors.DefectNotFound"/>)
/// — and asks <see cref="DuplicateDefectCascade.FindAsync"/> what a repair recorded now would
/// clear. Writes nothing.
/// </summary>
public sealed class GetDefectDuplicatesQueryHandler(
    IVehicleInspectionRepository repository,
    DuplicateDefectCascade duplicates)
    : IQueryHandler<GetDefectDuplicatesQuery, DefectDuplicatesResponse>
{
    public async Task<Result<DefectDuplicatesResponse>> Handle(
        GetDefectDuplicatesQuery query,
        CancellationToken cancellationToken)
    {
        var inspection = await repository.GetByIdAsync(query.InspectionId, cancellationToken);
        if (inspection is null)
        {
            return Result.Failure<DefectDuplicatesResponse>(InspectionErrors.NotFound);
        }

        var defect = inspection.FindDefect(query.Item);
        if (defect is null)
        {
            return Result.Failure<DefectDuplicatesResponse>(InspectionErrors.DefectNotFound);
        }

        // Already resolved: no resolve of it can happen, so it clears nothing.
        if (defect.IsResolved)
        {
            return Result.Success(new DefectDuplicatesResponse([]));
        }

        var found = await duplicates.FindAsync(
            inspection,
            defect.Item,
            DefectRepairCutoff.ForManualRepair(DateTimeOffset.UtcNow),
            cancellationToken);

        return Result.Success(new DefectDuplicatesResponse(
        [
            .. found.Select(d => new DefectDuplicateResponse(
                d.Inspection.Id,
                d.Defect.Item,
                d.Inspection.TripNumber,
                d.Defect.Severity.ToString(),
                d.Inspection.PerformedAt)),
        ]));
    }
}
