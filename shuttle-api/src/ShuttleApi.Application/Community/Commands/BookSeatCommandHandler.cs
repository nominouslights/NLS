using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Community.Commands;

internal sealed class BookSeatCommandHandler(
    ITripRepository tripRepository,
    INotificationService notificationService)
    : IRequestHandler<BookSeatCommand, BookSeatResult>
{
    private const int MinimumThreshold = 2;
    private const decimal OneWayFare = 90m;
    private const decimal ReturnFare = 170m;

    // Fixed route stops: direction "Outbound" = Thompson → Lynn Lake
    private static readonly (int Order, string Name, string? Address)[] OutboundStops =
    [
        (1, "Thompson", "Thompson, MB"),
        (2, "Lynn Lake", "Lynn Lake, MB")
    ];

    private static readonly (int Order, string Name, string? Address)[] InboundStops =
    [
        (1, "Lynn Lake", "Lynn Lake, MB"),
        (2, "Thompson", "Thompson, MB")
    ];

    public async Task<BookSeatResult> Handle(BookSeatCommand request, CancellationToken cancellationToken)
    {
        var fare = request.TripType.Equals("Return", StringComparison.OrdinalIgnoreCase)
            ? ReturnFare
            : OneWayFare;

        var departureTime = new DateTime(
            request.Date.Year, request.Date.Month, request.Date.Day,
            8, 0, 0, DateTimeKind.Utc);

        // Find or create community trip for this date+direction
        var trip = await tripRepository.FindCommunityTripAsync(
            request.Date, request.Direction, cancellationToken);

        if (trip is null)
        {
            var stops = request.Direction.Equals("Outbound", StringComparison.OrdinalIgnoreCase)
                ? OutboundStops
                : InboundStops;

            trip = Trip.Create(
                TripServiceType.Community,
                clientId: null,
                vehicleId: null,
                purchaseOrderNumber: null,
                vehicleType: null,
                scheduledAt: departureTime,
                notes: null,
                stops: stops,
                seatCapacity: 14,
                pricePerSeat: OneWayFare);

            await tripRepository.AddAsync(trip, cancellationToken);

            // Reload with tracking context
            trip = await tripRepository.GetByIdAsync(trip.Id, cancellationToken)
                ?? throw new InvalidOperationException("Failed to retrieve newly created trip.");
        }

        // Validate capacity
        var activeCount = trip.Passengers.Count(p =>
            p.PaymentStatus is PassengerPaymentStatus.Tentative
                or PassengerPaymentStatus.AwaitingPayment
                or PassengerPaymentStatus.Confirmed);

        if (trip.SeatCapacity.HasValue && activeCount >= trip.SeatCapacity.Value)
            throw new InvalidOperationException("No seats available on this departure.");

        // Generate unique booking reference NL-XXXX
        var reference = await GenerateUniqueReferenceAsync(cancellationToken);

        // Compute Thursday 6PM CT cutoff for the departure week
        var cutoff = ComputeThursdayCutoff(request.Date);

        var passenger = trip.AddPassenger(
            name: request.FullName,
            contactInfo: request.Email,
            seatNumber: null,
            paymentStatus: PassengerPaymentStatus.Tentative,
            bookingReference: reference,
            phone: request.Phone,
            email: request.Email,
            direction: request.Direction,
            cutoffDeadline: cutoff,
            bookedAt: DateTime.UtcNow,
            fare: fare);

        await tripRepository.UpdateAsync(trip, cancellationToken);

        var route = request.Direction.Equals("Outbound", StringComparison.OrdinalIgnoreCase)
            ? "Thompson → Lynn Lake"
            : "Lynn Lake → Thompson";

        var cutoffFormatted = TimeZoneInfo.ConvertTimeFromUtc(
            cutoff,
            TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"));

        var smsBody = $"Booking confirmed: {reference}. Route: {route} on {request.Date:MMM d, yyyy}. " +
                      $"Fare: ${fare}. Your seat is TENTATIVE. Payment due Thursday {cutoffFormatted:MMM d} at 6:00 PM CT. " +
                      $"Cash: Northern Link booth, Leaf Rapids Mall, every Thursday.";

        var emailSubject = $"Booking Held — {reference} | {route}";
        var emailBody = $"Your seat has been held.\n\nBooking: {reference}\nStatus: TENTATIVE\n" +
                        $"Route: {route}\nDate: {request.Date:dddd, MMMM d, yyyy}\nFare: ${fare}\n\n" +
                        $"Payment deadline: Thursday {cutoffFormatted:MMMM d, yyyy} at 6:00 PM CT\n\n" +
                        $"Cash payments accepted at the Northern Link booth, Leaf Rapids Mall, every Thursday.\n\n" +
                        $"No payment is being collected today. A payment request will be sent before the deadline.";

        await notificationService.SendSmsAsync(request.Phone, smsBody, cancellationToken);
        await notificationService.SendEmailAsync(request.Email, emailSubject, emailBody, cancellationToken);

        return new BookSeatResult(passenger.Id, reference, cutoff, fare, route);
    }

    private async Task<string> GenerateUniqueReferenceAsync(CancellationToken cancellationToken)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rng = Random.Shared;

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var suffix = new string(Enumerable.Range(0, 4)
                .Select(_ => chars[rng.Next(chars.Length)])
                .ToArray());

            var candidate = $"NL-{suffix}";

            if (!await tripRepository.BookingReferenceExistsAsync(candidate, cancellationToken))
                return candidate;
        }

        throw new InvalidOperationException("Unable to generate a unique booking reference.");
    }

    private static DateTime ComputeThursdayCutoff(DateOnly departureDate)
    {
        // Find the Thursday at or before the departure date
        var date = departureDate;
        while (date.DayOfWeek != DayOfWeek.Thursday)
            date = date.AddDays(-1);

        // Thursday 6:00 PM CST (UTC-6) = Thursday 24:00 UTC = Friday 00:00 UTC
        var friday = date.AddDays(1);
        return new DateTime(friday.Year, friday.Month, friday.Day, 0, 0, 0, DateTimeKind.Utc);
    }
}
