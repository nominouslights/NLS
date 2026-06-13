using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class SubmitPreInspectionCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<SubmitPreInspectionCommand>
{
    public async Task Handle(SubmitPreInspectionCommand request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip {request.TripId} not found.");

        var items = request.Items.Select(i => (i.ItemName, i.Category, i.Passed, i.Notes));

        trip.SubmitPreInspection(
            request.OdometerStart,
            request.FuelLevel,
            request.WeatherType,
            request.Temperature,
            request.RoadConditions,
            request.Visibility,
            request.RoadAdvisories,
            request.WeatherPulledAt,
            items);

        await tripRepository.UpdateAsync(trip, cancellationToken);
    }
}
