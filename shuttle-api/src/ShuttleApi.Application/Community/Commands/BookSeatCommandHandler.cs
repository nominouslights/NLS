using System.Runtime.InteropServices;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Community.Commands;

internal sealed class BookSeatCommandHandler(
    ITripRepository tripRepository,
    INotificationService notificationService)
    : IRequestHandler<BookSeatCommand, BookSeatResult>
{
    private const int MinimumThreshold = 2;

    // Fares per destination (one-way)
    private static decimal GetOneWayFare(string destination) =>
        destination.Equals("LeafRapids", StringComparison.OrdinalIgnoreCase) ? 100m : 120m;

    private static decimal GetReturnFare(string destination) =>
        GetOneWayFare(destination) * 2;

    // Stops by destination + direction
    private static readonly Dictionary<string, (int Order, string Name, string? Address)[]> OutboundStops = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LynnLake"] = [(1, "Thompson", "Thompson, MB"), (2, "Lynn Lake", "Lynn Lake, MB")],
        ["LeafRapids"] = [(1, "Thompson", "Thompson, MB"), (2, "Leaf Rapids", "Leaf Rapids, MB")],
    };

    private static readonly Dictionary<string, (int Order, string Name, string? Address)[]> InboundStops = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LynnLake"] = [(1, "Lynn Lake", "Lynn Lake, MB"), (2, "Thompson", "Thompson, MB")],
        ["LeafRapids"] = [(1, "Leaf Rapids", "Leaf Rapids, MB"), (2, "Thompson", "Thompson, MB")],
    };

    private static string DestinationDisplayName(string destination) =>
        destination.Equals("LeafRapids", StringComparison.OrdinalIgnoreCase) ? "Leaf Rapids" : "Lynn Lake";

    public async Task<BookSeatResult> Handle(BookSeatCommand request, CancellationToken cancellationToken)
    {
        var oneWayFare = GetOneWayFare(request.Destination);
        var fare = request.TripType.Equals("Return", StringComparison.OrdinalIgnoreCase)
            ? GetReturnFare(request.Destination)
            : oneWayFare;

        var destinationName = DestinationDisplayName(request.Destination);

        var departureTime = new DateTime(
            request.Date.Year, request.Date.Month, request.Date.Day,
            8, 0, 0, DateTimeKind.Utc);

        var trip = await tripRepository.FindCommunityTripAsync(
            request.Date, request.Direction, request.Destination, cancellationToken);

        if (trip is null)
        {
            var stopsDict = request.Direction.Equals("Outbound", StringComparison.OrdinalIgnoreCase)
                ? OutboundStops
                : InboundStops;

            var stops = stopsDict.TryGetValue(request.Destination, out var s) ? s : stopsDict["LynnLake"];

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
                pricePerSeat: oneWayFare);

            await tripRepository.AddAsync(trip, cancellationToken);

            trip = await tripRepository.GetByIdAsync(trip.Id, cancellationToken)
                ?? throw new InvalidOperationException("Failed to retrieve newly created trip.");
        }

        var activeCount = trip.Passengers.Count(p =>
            p.PaymentStatus is PassengerPaymentStatus.Tentative
                or PassengerPaymentStatus.AwaitingPayment
                or PassengerPaymentStatus.Confirmed);

        if (trip.SeatCapacity.HasValue && activeCount >= trip.SeatCapacity.Value)
            throw new InvalidOperationException("No seats available on this departure.");

        var reference = await GenerateUniqueReferenceAsync(cancellationToken);
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
            ? $"Thompson → {destinationName}"
            : $"{destinationName} → Thompson";

        var tzId = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "Central Standard Time"
            : "America/Chicago";
        var cutoffFormatted = TimeZoneInfo.ConvertTimeFromUtc(
            cutoff,
            TimeZoneInfo.FindSystemTimeZoneById(tzId));

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

        return new BookSeatResult(
            PassengerId: passenger.Id,
            BookingReference: reference,
            CutoffDeadline: cutoff,
            Fare: fare,
            Route: route,
            FullName: request.FullName,
            Phone: request.Phone,
            Email: request.Email,
            Direction: request.Direction,
            TripType: request.TripType,
            DepartureDate: request.Date,
            Status: "Tentative",
            BookedAt: passenger.BookedAt);
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
        var date = departureDate;
        while (date.DayOfWeek != DayOfWeek.Thursday)
            date = date.AddDays(-1);

        var friday = date.AddDays(1);
        return new DateTime(friday.Year, friday.Month, friday.Day, 0, 0, 0, DateTimeKind.Utc);
    }
}
