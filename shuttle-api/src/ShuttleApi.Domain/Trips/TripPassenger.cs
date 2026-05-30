using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Trips;

public sealed class TripPassenger : Entity<Guid>
{
    public Guid TripId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? ContactInfo { get; private set; }
    public int? SeatNumber { get; private set; }
    public PassengerPaymentStatus PaymentStatus { get; private set; }

    // Booking-specific fields (populated for community bookings)
    public string? BookingReference { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Direction { get; private set; }
    public DateTime? CutoffDeadline { get; private set; }
    public DateTime BookedAt { get; private set; }
    public decimal? Fare { get; private set; }

    private TripPassenger() { }

    public static TripPassenger Create(
        Guid tripId,
        string name,
        string? contactInfo,
        int? seatNumber,
        PassengerPaymentStatus paymentStatus,
        string? bookingReference = null,
        string? phone = null,
        string? email = null,
        string? direction = null,
        DateTime? cutoffDeadline = null,
        DateTime? bookedAt = null,
        decimal? fare = null)
    {
        return new TripPassenger
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            Name = name,
            ContactInfo = contactInfo,
            SeatNumber = seatNumber,
            PaymentStatus = paymentStatus,
            BookingReference = bookingReference,
            Phone = phone,
            Email = email,
            Direction = direction,
            CutoffDeadline = cutoffDeadline,
            BookedAt = bookedAt ?? DateTime.UtcNow,
            Fare = fare
        };
    }

    public void UpdatePaymentStatus(PassengerPaymentStatus newStatus)
    {
        var valid = (PaymentStatus, newStatus) switch
        {
            (PassengerPaymentStatus.Tentative, PassengerPaymentStatus.AwaitingPayment) => true,
            (PassengerPaymentStatus.Tentative, PassengerPaymentStatus.Confirmed) => true,
            (PassengerPaymentStatus.Tentative, PassengerPaymentStatus.Cancelled) => true,
            (PassengerPaymentStatus.AwaitingPayment, PassengerPaymentStatus.Confirmed) => true,
            (PassengerPaymentStatus.AwaitingPayment, PassengerPaymentStatus.Released) => true,
            (PassengerPaymentStatus.AwaitingPayment, PassengerPaymentStatus.Cancelled) => true,
            (PassengerPaymentStatus.Confirmed, PassengerPaymentStatus.Cancelled) => true,
            _ => false
        };

        Guard.Against(!valid,
            $"Cannot transition seat status from {PaymentStatus} to {newStatus}.");

        PaymentStatus = newStatus;
    }
}
