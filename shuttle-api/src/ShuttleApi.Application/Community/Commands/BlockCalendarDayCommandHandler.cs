using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.CommunityCalendar;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Community.Commands;

internal sealed class BlockCalendarDayCommandHandler(
    ICommunityCalendarBlockRepository blockRepository,
    ITripRepository tripRepository,
    INotificationService notificationService)
    : IRequestHandler<BlockCalendarDayCommand, BlockCalendarDayResult>
{
    public async Task<BlockCalendarDayResult> Handle(
        BlockCalendarDayCommand request, CancellationToken cancellationToken)
    {
        var existing = await blockRepository.GetByDateAsync(request.Date, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException($"{request.Date:yyyy-MM-dd} is already blocked.");

        var block = CommunityCalendarBlock.Create(request.Date, request.Reason);
        await blockRepository.AddAsync(block, cancellationToken);

        // Cancel all active passengers on trips for this date
        var trips = await tripRepository.GetByDateRangeAsync(
            request.Date, request.Date, TripServiceType.Community, cancellationToken);

        var cancelledCount = 0;

        foreach (var trip in trips)
        {
            foreach (var passenger in trip.Passengers
                .Where(p => p.PaymentStatus is
                    PassengerPaymentStatus.Tentative or
                    PassengerPaymentStatus.AwaitingPayment or
                    PassengerPaymentStatus.Confirmed)
                .ToList())
            {
                trip.UpdatePassengerPaymentStatus(passenger.Id, PassengerPaymentStatus.Cancelled);
                cancelledCount++;

                var route = passenger.Direction?.Equals("Outbound", StringComparison.OrdinalIgnoreCase) == true
                    ? "Thompson → Lynn Lake"
                    : "Lynn Lake → Thompson";

                var smsBody = $"NOTICE: Your Northern Link booking {passenger.BookingReference} " +
                              $"({route} on {request.Date:MMM d, yyyy}) has been cancelled. " +
                              "No payment will be collected. We apologize for the inconvenience.";

                var emailBody = $"Your booking has been cancelled.\n\nBooking: {passenger.BookingReference}\n" +
                                $"Route: {route}\nDate: {request.Date:dddd, MMMM d, yyyy}\n\n" +
                                "No payment will be collected. We apologize for the inconvenience.";

                await notificationService.SendSmsAsync(
                    passenger.Phone ?? string.Empty, smsBody, cancellationToken);
                await notificationService.SendEmailAsync(
                    passenger.Email ?? string.Empty,
                    $"Booking Cancelled — {passenger.BookingReference}",
                    emailBody,
                    cancellationToken);
            }

            await tripRepository.UpdateAsync(trip, cancellationToken);
        }

        return new BlockCalendarDayResult(cancelledCount);
    }
}
