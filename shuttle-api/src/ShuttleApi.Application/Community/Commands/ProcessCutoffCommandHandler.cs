using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Community.Commands;

internal sealed class ProcessCutoffCommandHandler(
    ITripRepository tripRepository,
    INotificationService notificationService)
    : IRequestHandler<ProcessCutoffCommand, ProcessCutoffResult>
{
    private const int MinimumThreshold = 2;

    public async Task<ProcessCutoffResult> Handle(
        ProcessCutoffCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var windowEnd = today.AddDays(14);

        var trips = await tripRepository.GetByDateRangeAsync(
            today, windowEnd, TripServiceType.Community, cancellationToken);

        var openedCount = 0;
        var releasedCount = 0;
        var tripsCancelledCount = 0;

        foreach (var trip in trips)
        {
            // Transition Tentative → AwaitingPayment: cutoff opens within the next hour
            foreach (var passenger in trip.Passengers
                .Where(p => p.PaymentStatus == PassengerPaymentStatus.Tentative
                    && p.CutoffDeadline.HasValue
                    && p.CutoffDeadline.Value <= now.AddHours(1)
                    && p.CutoffDeadline.Value > now)
                .ToList())
            {
                trip.UpdatePassengerPaymentStatus(passenger.Id, PassengerPaymentStatus.AwaitingPayment);
                openedCount++;

                var route = passenger.Direction?.Equals("Outbound", StringComparison.OrdinalIgnoreCase) == true
                    ? "Thompson → Lynn Lake"
                    : "Lynn Lake → Thompson";

                var cutoffLocal = TimeZoneInfo.ConvertTimeFromUtc(
                    passenger.CutoffDeadline!.Value,
                    TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"));

                var smsBody = $"PAYMENT DUE: Booking {passenger.BookingReference} ({route}). " +
                              $"Pay by {cutoffLocal:MMM d} at 6:00 PM CT. " +
                              "Cash: Northern Link booth, Leaf Rapids Mall.";

                var emailBody = $"Payment is due for your Northern Link booking.\n\n" +
                                $"Booking: {passenger.BookingReference}\nRoute: {route}\n" +
                                $"Deadline: {cutoffLocal:MMMM d, yyyy} at 6:00 PM CT\n\n" +
                                "Cash payments accepted at the Northern Link booth, Leaf Rapids Mall, every Thursday.";

                await notificationService.SendSmsAsync(
                    passenger.Phone ?? string.Empty, smsBody, cancellationToken);
                await notificationService.SendEmailAsync(
                    passenger.Email ?? string.Empty,
                    $"Payment Due — {passenger.BookingReference}",
                    emailBody,
                    cancellationToken);
            }

            // Transition AwaitingPayment → Released: cutoff has passed
            foreach (var passenger in trip.Passengers
                .Where(p => p.PaymentStatus == PassengerPaymentStatus.AwaitingPayment
                    && p.CutoffDeadline.HasValue
                    && p.CutoffDeadline.Value <= now)
                .ToList())
            {
                trip.UpdatePassengerPaymentStatus(passenger.Id, PassengerPaymentStatus.Released);
                releasedCount++;

                var route = passenger.Direction?.Equals("Outbound", StringComparison.OrdinalIgnoreCase) == true
                    ? "Thompson → Lynn Lake"
                    : "Lynn Lake → Thompson";

                var smsBody = $"NOTICE: Booking {passenger.BookingReference} ({route}) has been released. " +
                              "Payment was not received before the deadline. Your seat is no longer held.";

                await notificationService.SendSmsAsync(
                    passenger.Phone ?? string.Empty, smsBody, cancellationToken);
                await notificationService.SendEmailAsync(
                    passenger.Email ?? string.Empty,
                    $"Booking Released — {passenger.BookingReference}",
                    $"Your booking {passenger.BookingReference} ({route}) has been released. " +
                    "Payment was not received before the Thursday 6:00 PM CT deadline.",
                    cancellationToken);
            }

            await tripRepository.UpdateAsync(trip, cancellationToken);

            // Viability check: trip departs within 12 hours with <2 confirmed passengers
            var tripDate = DateOnly.FromDateTime(trip.ScheduledAt);
            var hoursUntilDeparture = (trip.ScheduledAt - now).TotalHours;

            if (hoursUntilDeparture is > 0 and <= 12)
            {
                var confirmedCount = trip.Passengers.Count(p =>
                    p.PaymentStatus == PassengerPaymentStatus.Confirmed);

                if (confirmedCount < MinimumThreshold)
                {
                    // Cancel the trip — set all active passengers to Cancelled
                    foreach (var passenger in trip.Passengers
                        .Where(p => p.PaymentStatus is
                            PassengerPaymentStatus.Tentative or
                            PassengerPaymentStatus.AwaitingPayment or
                            PassengerPaymentStatus.Confirmed)
                        .ToList())
                    {
                        trip.UpdatePassengerPaymentStatus(passenger.Id, PassengerPaymentStatus.Cancelled);

                        var route = passenger.Direction?.Equals("Outbound", StringComparison.OrdinalIgnoreCase) == true
                            ? "Thompson → Lynn Lake"
                            : "Lynn Lake → Thompson";

                        var smsBody = $"CANCELLED: Booking {passenger.BookingReference} ({route} on " +
                                      $"{tripDate:MMM d, yyyy}) has been cancelled — minimum passenger threshold not met. " +
                                      "No payment will be collected.";

                        await notificationService.SendSmsAsync(
                            passenger.Phone ?? string.Empty, smsBody, cancellationToken);
                        await notificationService.SendEmailAsync(
                            passenger.Email ?? string.Empty,
                            $"Trip Cancelled — {passenger.BookingReference}",
                            $"Your booking {passenger.BookingReference} has been cancelled.\n\n" +
                            "The departure did not meet the minimum passenger threshold.\n" +
                            "No payment will be collected. We apologize for the inconvenience.",
                            cancellationToken);
                    }

                    trip.UpdateStatus(TripStatus.Cancelled);
                    await tripRepository.UpdateAsync(trip, cancellationToken);
                    tripsCancelledCount++;
                }
            }
        }

        return new ProcessCutoffResult(openedCount, releasedCount, tripsCancelledCount);
    }
}
