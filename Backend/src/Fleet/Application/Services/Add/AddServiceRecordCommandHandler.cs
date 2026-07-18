using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Services;
using NorthernLink.Fleet.Domain.Vehicles;

namespace NorthernLink.Fleet.Application.Services.Add;

public sealed class AddServiceRecordCommandHandler(IServiceRecordRepository repository)
    : ICommandHandler<AddServiceRecordCommand, Guid>
{
    public async Task<Result<Guid>> Handle(AddServiceRecordCommand command, CancellationToken cancellationToken)
    {
        if (!await repository.VehicleExistsAsync(command.VehicleId, cancellationToken))
        {
            return Result.Failure<Guid>(VehicleErrors.NotFound);
        }

        var sequence = await repository.NextSequenceAsync(command.TenantId, cancellationToken);
        var number = $"SVC-{sequence}";

        var parts = command.PartsUsed
            .Where(p => !string.IsNullOrWhiteSpace(p.Sku))
            .Select(p => new ServicePart { Sku = p.Sku.Trim(), Qty = p.Qty <= 0 ? 1 : p.Qty })
            .ToList();

        var recordResult = ServiceRecord.Log(
            command.TenantId,
            command.VehicleId,
            number,
            command.Date,
            command.PerformedBy,
            command.Category,
            command.OdometerKm,
            command.ItemsChanged,
            command.Reason,
            parts,
            command.LaborHours,
            command.CostCad,
            command.WorkOrderId,
            command.Notes);

        if (recordResult.IsFailure)
        {
            return Result.Failure<Guid>(recordResult.Error);
        }

        repository.Add(recordResult.Value);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(recordResult.Value.Id);
    }
}
