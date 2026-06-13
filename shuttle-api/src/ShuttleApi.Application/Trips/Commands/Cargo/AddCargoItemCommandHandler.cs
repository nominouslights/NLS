using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class AddCargoItemCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<AddCargoItemCommand, AddCargoItemResult>
{
    public async Task<AddCargoItemResult> Handle(AddCargoItemCommand request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip {request.TripId} not found.");

        var item = trip.AddCargoItem(
            request.CargoType, request.Description, request.Quantity,
            request.WeightKg, request.Charge, request.IsHazmat, request.IsSecured);

        await tripRepository.UpdateAsync(trip, cancellationToken);

        return new AddCargoItemResult(item.Id);
    }
}
