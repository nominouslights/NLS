using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Trips;

public sealed class TripStop : Entity<Guid>
{
    public Guid TripId { get; private set; }
    public int SequenceOrder { get; private set; }
    public string LocationName { get; private set; } = string.Empty;
    public string? Address { get; private set; }

    private TripStop() { }

    public static TripStop Create(Guid tripId, int sequenceOrder, string locationName, string? address)
    {
        return new TripStop
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            SequenceOrder = sequenceOrder,
            LocationName = locationName,
            Address = address
        };
    }

    internal void UpdateSequenceOrder(int newOrder) => SequenceOrder = newOrder;
}
