using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Shipments;

namespace NorthernLink.Trips.Application.Shipments.Register;

public sealed class RegisterShipmentCommandHandler(
    IShipmentRepository shipments,
    IShipmentNumberGenerator numbers,
    IClientLookupRepository clientLookup)
    : ICommandHandler<RegisterShipmentCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegisterShipmentCommand command, CancellationToken cancellationToken)
    {
        // The client is validated against the SAME client_lookup replica trips already use — no
        // new cross-module plumbing. It is validated, never defaulted: a shipment's payer is its
        // own fact, and there is no trip in scope here to borrow one from even if we wanted to.
        var details = command.Details;
        if (details.ClientId is { } clientId)
        {
            var client = await clientLookup.GetAsync(clientId, cancellationToken);
            if (client is null)
            {
                return Result.Failure<Guid>(ShipmentErrors.ClientNotFound);
            }

            details = details with { ClientName = client.Name };
        }

        var number = await numbers.NextAsync(command.TenantId, cancellationToken);
        var result = Shipment.Register(command.TenantId, number, details, command.Source, command.EnteredBy);
        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        shipments.Add(result.Value);
        await shipments.SaveChangesAsync(cancellationToken);
        return Result.Success(result.Value.Id);
    }
}
