using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Shipments;

namespace NorthernLink.Trips.Application.Shipments.SetBilling;

public sealed class SetShipmentBillingCommandHandler(
    IShipmentRepository shipments,
    IClientLookupRepository clientLookup)
    : ICommandHandler<SetShipmentBillingCommand>
{
    public async Task<Result> Handle(SetShipmentBillingCommand command, CancellationToken cancellationToken)
    {
        var shipment = await shipments.GetByIdAsync(command.ShipmentId, cancellationToken);
        if (shipment is null)
        {
            return Result.Failure(ShipmentErrors.NotFound);
        }

        // Validated against the existing client_lookup replica — the same one trip assignment
        // uses — and the name snapshotted from it, matching Trip.ClientName's semantics.
        string? clientName = null;
        if (command.ClientId is { } clientId)
        {
            var client = await clientLookup.GetAsync(clientId, cancellationToken);
            if (client is null)
            {
                return Result.Failure(ShipmentErrors.ClientNotFound);
            }

            clientName = client.Name;
        }

        var result = shipment.SetBilling(
            command.ClientId, clientName, command.PoNumber, command.ChargeCad, command.PaymentMethod);

        if (result.IsFailure)
        {
            return result;
        }

        await shipments.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
