using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Shipments;

namespace NorthernLink.Trips.Application.Shipments.RecordDelivery;

public sealed class RecordShipmentDeliveryCommandHandler(IShipmentRepository shipments)
    : ICommandHandler<RecordShipmentDeliveryCommand>
{
    public async Task<Result> Handle(RecordShipmentDeliveryCommand command, CancellationToken cancellationToken)
    {
        var shipment = await shipments.GetByIdAsync(command.ShipmentId, cancellationToken);
        if (shipment is null)
        {
            return Result.Failure(ShipmentErrors.NotFound);
        }

        var result = shipment.RecordDelivery(command.AtUtc, command.ReceivedBy, command.Note);
        if (result.IsFailure)
        {
            return result;
        }

        await shipments.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
