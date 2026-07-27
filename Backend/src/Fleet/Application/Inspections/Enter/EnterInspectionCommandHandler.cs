using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Application.Inspections.Enter;

public sealed class EnterInspectionCommandHandler(IVehicleInspectionRepository repository)
    : ICommandHandler<EnterInspectionCommand, Guid>
{
    public async Task<Result<Guid>> Handle(EnterInspectionCommand command, CancellationToken cancellationToken)
    {
        var checklist = command.Checklist
            .Select(c => new InspectionChecklistItem { Group = c.Group, Item = c.Item, Passed = c.Passed })
            .ToList();

        var defects = command.Defects
            .Select(d => new InspectionDefect { Item = d.Item, Severity = d.Severity, Note = d.Note })
            .ToList();

        var inspectionResult = VehicleInspection.Enter(
            command.TenantId,
            command.Source,
            command.Type,
            command.TripNumber,
            command.VehicleId,
            command.Unit,
            command.DriverName,
            command.EnteredBy,
            // Normalize client-supplied timestamps to UTC before they reach the aggregate:
            // both PerformedAt and CertifiedAt map to `timestamp with time zone`, and Npgsql
            // rejects a DateTimeOffset with a non-zero offset for that type. ToUniversalTime()
            // preserves the instant (an offset of default(DateTimeOffset) is already UTC, so it
            // is a harmless no-op there); the wall-clock offset the client sent is not persisted.
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

        if (inspectionResult.IsFailure)
        {
            return Result.Failure<Guid>(inspectionResult.Error);
        }

        repository.Add(inspectionResult.Value);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(inspectionResult.Value.Id);
    }
}
