using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Application.Inspections.Update;

/// <summary>
/// Amends an existing inspection. Loads it tenant-filtered (a cross-tenant id resolves to null
/// and yields <see cref="InspectionErrors.NotFound"/>), then re-runs the aggregate's own
/// validation and result derivation via <see cref="VehicleInspection.Amend"/>. No duplicate
/// check is needed: the record's <c>Type</c> and <c>TripNumber</c> are fixed. Timestamps are
/// normalized to UTC exactly as on the enter path (Npgsql rejects a non-zero offset for
/// <c>timestamp with time zone</c>).
/// </summary>
public sealed class UpdateInspectionCommandHandler(IVehicleInspectionRepository repository)
    : ICommandHandler<UpdateInspectionCommand>
{
    public async Task<Result> Handle(UpdateInspectionCommand command, CancellationToken cancellationToken)
    {
        var inspection = await repository.GetByIdAsync(command.InspectionId, cancellationToken);
        if (inspection is null)
        {
            return Result.Failure(InspectionErrors.NotFound);
        }

        var checklist = command.Checklist
            .Select(c => new InspectionChecklistItem { Group = c.Group, Item = c.Item, Passed = c.Passed })
            .ToList();

        var defects = command.Defects
            .Select(d => new InspectionDefect { Item = d.Item, Severity = d.Severity, Note = d.Note })
            .ToList();

        var amendResult = inspection.Amend(
            command.Source,
            command.VehicleId,
            command.Unit,
            command.DriverName,
            command.EnteredBy,
            command.PerformedAt.ToUniversalTime(),
            command.OdometerKm,
            checklist,
            defects,
            command.Weather,
            command.TemperatureC,
            command.RoadConditions,
            command.Visibility,
            command.RoadAdvisories,
            command.FuelLevel,
            command.Issues,
            command.Attestations,
            command.DriverSignatureName,
            command.CertifiedAt?.ToUniversalTime(),
            command.FuelAdded,
            command.FuelLitres,
            command.FuelCostCad);

        if (amendResult.IsFailure)
        {
            return amendResult;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
