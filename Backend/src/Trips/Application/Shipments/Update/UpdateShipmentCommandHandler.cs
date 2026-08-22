using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Shipments;

namespace NorthernLink.Trips.Application.Shipments.Update;

public sealed class UpdateShipmentCommandHandler(
    IShipmentRepository shipments,
    IClientLookupRepository clientLookup)
    : ICommandHandler<UpdateShipmentCommand>
{
    public async Task<Result> Handle(UpdateShipmentCommand command, CancellationToken cancellationToken)
    {
        var shipment = await shipments.GetByIdAsync(command.ShipmentId, cancellationToken);
        if (shipment is null)
        {
            return Result.Failure(ShipmentErrors.NotFound);
        }

        var details = command.Details;
        if (details.ClientId is { } clientId)
        {
            var client = await clientLookup.GetAsync(clientId, cancellationToken);
            if (client is null)
            {
                return Result.Failure(ShipmentErrors.ClientNotFound);
            }

            details = details with { ClientName = client.Name };
        }

        var result = shipment.UpdateDetails(details, command.Source, command.EnteredBy);
        if (result.IsFailure)
        {
            return result;
        }

        await shipments.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
