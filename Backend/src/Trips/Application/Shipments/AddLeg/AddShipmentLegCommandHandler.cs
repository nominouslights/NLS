using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Shipments;

namespace NorthernLink.Trips.Application.Shipments.AddLeg;

public sealed class AddShipmentLegCommandHandler(
    IShipmentRepository shipments,
    ITripRepository trips)
    : ICommandHandler<AddShipmentLegCommand>
{
    public async Task<Result> Handle(AddShipmentLegCommand command, CancellationToken cancellationToken)
    {
        var shipment = await shipments.GetByIdAsync(command.ShipmentId, cancellationToken);
        if (shipment is null)
        {
            return Result.Failure(ShipmentErrors.NotFound);
        }

        var trip = await trips.GetByIdAsync(command.TripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure(ShipmentErrors.TripNotFound);
        }

        // A run that has already happened cannot take on new freight. Note there is deliberately
        // no IsEmptyLeg check: filling a deadhead with parcels is the point of having one.
        if (trip.IsOperationallyClosed)
        {
            return Result.Failure(ShipmentErrors.TripOperationallyClosed);
        }

        // Corridor defaults come from the trip, but the client never does.
        var result = shipment.AddLeg(
            trip.Id,
            trip.TripNumber,
            trip.ServiceDate,
            command.FromStopId,
            command.FromName ?? trip.Origin,
            command.ToStopId,
            command.ToName ?? trip.Destination);

        if (result.IsFailure)
        {
            return result;
        }

        await shipments.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
