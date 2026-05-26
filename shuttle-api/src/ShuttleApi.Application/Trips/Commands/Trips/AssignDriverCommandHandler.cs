using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Drivers;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class AssignDriverCommandHandler(
    ITripRepository tripRepository,
    IDriverRepository driverRepository)
    : IRequestHandler<AssignDriverCommand>
{
    public async Task Handle(AssignDriverCommand request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip {request.TripId} not found.");

        _ = await driverRepository.GetByIdAsync(request.DriverId, cancellationToken)
            ?? throw new NotFoundException($"Driver {request.DriverId} not found.");

        trip.AssignDriver(request.DriverId, request.VehicleType);

        await tripRepository.UpdateAsync(trip, cancellationToken);
    }
}
