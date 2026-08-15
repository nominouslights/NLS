using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Shipments;

namespace NorthernLink.Trips.Application.Shipments.RecordLegDrop;

public sealed class RecordShipmentLegDropCommandHandler(IShipmentRepository shipments)
    : ICommandHandler<RecordShipmentLegDropCommand>
{
    public async Task<Result> Handle(RecordShipmentLegDropCommand command, CancellationToken cancellationToken)
    {
        var shipment = await shipments.GetByIdAsync(command.ShipmentId, cancellationToken);
        if (shipment is null)
        {
            return Result.Failure(ShipmentErrors.NotFound);
        }

        var result = shipment.RecordLegDrop(command.Sequence, command.AtUtc, command.By);
        if (result.IsFailure)
        {
            return result;
        }

        await shipments.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
