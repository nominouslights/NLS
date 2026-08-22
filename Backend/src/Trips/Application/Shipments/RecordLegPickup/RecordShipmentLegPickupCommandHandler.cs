using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Shipments;

namespace NorthernLink.Trips.Application.Shipments.RecordLegPickup;

public sealed class RecordShipmentLegPickupCommandHandler(IShipmentRepository shipments)
    : ICommandHandler<RecordShipmentLegPickupCommand>
{
    public async Task<Result> Handle(RecordShipmentLegPickupCommand command, CancellationToken cancellationToken)
    {
        var shipment = await shipments.GetByIdAsync(command.ShipmentId, cancellationToken);
        if (shipment is null)
        {
            return Result.Failure(ShipmentErrors.NotFound);
        }

        var result = shipment.RecordLegPickup(command.Sequence, command.AtUtc, command.By);
        if (result.IsFailure)
        {
            return result;
        }

        await shipments.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
