using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Shipments;

namespace NorthernLink.Trips.Application.Shipments.GetById;

public sealed class GetShipmentByIdQueryHandler(IShipmentReadService shipments)
    : IQueryHandler<GetShipmentByIdQuery, ShipmentResponse>
{
    public async Task<Result<ShipmentResponse>> Handle(
        GetShipmentByIdQuery query,
        CancellationToken cancellationToken)
    {
        var shipment = await shipments.GetByIdAsync(query.ShipmentId, cancellationToken);
        return shipment is null
            ? Result.Failure<ShipmentResponse>(ShipmentErrors.NotFound)
            : Result.Success(shipment);
    }
}
