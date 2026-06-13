using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Queries.FuelEntries;

internal sealed class GetVehicleFuelEntriesQueryHandler(IVehicleRepository vehicleRepository)
    : IRequestHandler<GetVehicleFuelEntriesQuery, IReadOnlyList<FuelEntryResult>>
{
    public async Task<IReadOnlyList<FuelEntryResult>> Handle(
        GetVehicleFuelEntriesQuery request,
        CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdWithRecordsAsync(request.VehicleId, cancellationToken)
            ?? throw new NotFoundException($"Vehicle {request.VehicleId} not found.");

        return vehicle.FuelEntries
            .OrderByDescending(e => e.FuelledAt)
            .Select(e => new FuelEntryResult(
                e.Id,
                e.FuelledAt,
                e.FuelLitres,
                e.TotalCostDollars,
                e.OdometerAtFuelling,
                e.ReceiptPhotoUrl,
                e.Notes,
                e.CreatedAt))
            .ToList();
    }
}
