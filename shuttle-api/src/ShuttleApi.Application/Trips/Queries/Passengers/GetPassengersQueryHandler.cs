using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class GetPassengersQueryHandler(ITripRepository tripRepository)
    : IRequestHandler<GetPassengersQuery, IReadOnlyList<PassengerResult>>
{
    public async Task<IReadOnlyList<PassengerResult>> Handle(GetPassengersQuery request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip {request.TripId} not found.");

        return trip.Passengers
            .Select(p => new PassengerResult(
                p.Id,
                p.TripId,
                p.Name,
                p.ContactInfo,
                p.SeatNumber,
                p.PaymentStatus.ToString(),
                p.BookingReference,
                p.Phone,
                p.Email,
                p.Direction,
                p.CutoffDeadline,
                p.BookedAt,
                p.Fare))
            .ToList();
    }
}
