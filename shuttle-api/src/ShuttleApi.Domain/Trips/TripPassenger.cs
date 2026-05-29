using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Trips;

public sealed class TripPassenger : Entity<Guid>
{
    public Guid TripId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? ContactInfo { get; private set; }
    public int? SeatNumber { get; private set; }
    public PassengerPaymentStatus PaymentStatus { get; private set; }

    private TripPassenger() { }

    public static TripPassenger Create(
        Guid tripId,
        string name,
        string? contactInfo,
        int? seatNumber,
        PassengerPaymentStatus paymentStatus)
    {
        return new TripPassenger
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            Name = name,
            ContactInfo = contactInfo,
            SeatNumber = seatNumber,
            PaymentStatus = paymentStatus
        };
    }

    public void UpdatePaymentStatus(PassengerPaymentStatus status) =>
        PaymentStatus = status;
}
