using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Trips;

public sealed class TripPassengerEmailLog : Entity<Guid>
{
    private TripPassengerEmailLog() { }

    public static TripPassengerEmailLog Record(
        Guid passengerId, string recipientEmail, string direction, bool isTest) =>
        new()
        {
            Id = Guid.NewGuid(),
            TripPassengerId = passengerId,
            RecipientEmail = recipientEmail,
            Direction = direction,
            SentAt = DateTime.UtcNow,
            IsTest = isTest
        };

    public Guid TripPassengerId { get; private set; }
    public string RecipientEmail { get; private set; } = default!;
    public string Direction { get; private set; } = default!;
    public DateTime SentAt { get; private set; }
    public bool IsTest { get; private set; }
}
