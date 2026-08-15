using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Shipments;

namespace NorthernLink.Trips.Application.Shipments.BulkAssign;

public sealed class BulkAssignShipmentsCommandHandler(
    IShipmentRepository shipments,
    ITripRepository trips)
    : ICommandHandler<BulkAssignShipmentsCommand, BulkAssignResult>
{
    public async Task<Result<BulkAssignResult>> Handle(
        BulkAssignShipmentsCommand command,
        CancellationToken cancellationToken)
    {
        var trip = await trips.GetByIdAsync(command.TripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure<BulkAssignResult>(ShipmentErrors.TripNotFound);
        }

        if (trip.IsOperationallyClosed)
        {
            return Result.Failure<BulkAssignResult>(ShipmentErrors.TripOperationallyClosed);
        }

        var loaded = await shipments.GetByIdsAsync(command.ShipmentIds, cancellationToken);
        var byId = loaded.ToDictionary(s => s.Id);

        var failures = new List<BulkAssignFailure>();
        var assigned = 0;

        foreach (var shipmentId in command.ShipmentIds)
        {
            if (!byId.TryGetValue(shipmentId, out var shipment))
            {
                failures.Add(new BulkAssignFailure(
                    shipmentId, ShipmentErrors.NotFound.Code, ShipmentErrors.NotFound.Message));
                continue;
            }

            var result = shipment.AddLeg(
                trip.Id, trip.TripNumber, trip.ServiceDate, null, trip.Origin, null, trip.Destination);

            if (result.IsFailure)
            {
                failures.Add(new BulkAssignFailure(shipmentId, result.Error.Code, result.Error.Message));
                continue;
            }

            assigned++;
        }

        // One save for everything that succeeded — the per-item failures above never reached the
        // aggregate, so nothing partial is pending.
        if (assigned > 0)
        {
            await shipments.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new BulkAssignResult(assigned, failures));
    }
}
