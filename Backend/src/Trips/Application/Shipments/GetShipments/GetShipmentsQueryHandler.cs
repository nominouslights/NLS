using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Shipments;

namespace NorthernLink.Trips.Application.Shipments.GetShipments;

public sealed class GetShipmentsQueryHandler(IShipmentReadService shipments)
    : IQueryHandler<GetShipmentsQuery, ShipmentPageResponse>
{
    public async Task<Result<ShipmentPageResponse>> Handle(
        GetShipmentsQuery query,
        CancellationToken cancellationToken)
    {
        // Matched against the enum's NAMES, ordinally — never Enum.TryParse, which would also
        // accept "?status=2" and bind it by ordinal. Same reasoning as the trips list.
        if (query.Filter.Status is { } status
            && !Enum.GetNames<ShipmentStatus>().Contains(status, StringComparer.Ordinal))
        {
            return Result.Failure<ShipmentPageResponse>(ShipmentErrors.InvalidStatusFilter);
        }

        if (query.Filter.Kind is { } kind
            && !Enum.GetNames<ShipmentKind>().Contains(kind, StringComparer.Ordinal))
        {
            return Result.Failure<ShipmentPageResponse>(ShipmentErrors.InvalidKindFilter);
        }

        return Result.Success(await shipments.GetShipmentsAsync(query.Filter, cancellationToken));
    }
}
