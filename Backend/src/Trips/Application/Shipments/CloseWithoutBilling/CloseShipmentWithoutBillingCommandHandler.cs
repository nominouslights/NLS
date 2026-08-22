using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Shipments;

namespace NorthernLink.Trips.Application.Shipments.CloseWithoutBilling;

public sealed class CloseShipmentWithoutBillingCommandHandler(IShipmentRepository shipments)
    : ICommandHandler<CloseShipmentWithoutBillingCommand>
{
    public async Task<Result> Handle(CloseShipmentWithoutBillingCommand command, CancellationToken cancellationToken)
    {
        var shipment = await shipments.GetByIdAsync(command.ShipmentId, cancellationToken);
        if (shipment is null)
        {
            return Result.Failure(ShipmentErrors.NotFound);
        }

        var result = shipment.CloseWithoutBilling(command.Reason);
        if (result.IsFailure)
        {
            return result;
        }

        await shipments.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
