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

        var inspectionResult = VehicleInspection.EnterByDispatcher(
            command.TenantId,
            command.Unit,
            command.Type,
            command.DriverName,
            command.EnteredBy,
            command.PerformedAt,
            command.OdometerKm,
            checklist,
            defects);

        if (inspectionResult.IsFailure)
        {
            return Result.Failure<Guid>(inspectionResult.Error);
        }

        repository.Add(inspectionResult.Value);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(inspectionResult.Value.Id);
    }
}
