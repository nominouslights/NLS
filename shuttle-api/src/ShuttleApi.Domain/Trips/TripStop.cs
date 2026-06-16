using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Trips;

public sealed class TripStop : Entity<Guid>
{
    public Guid TripId { get; private set; }
    public int SequenceOrder { get; private set; }
    public string LocationName { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public DateTime? ArrivedAt { get; private set; }
    public DateTime? DepartedAt { get; private set; }

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

    public void SetArrived(DateTime at)
    {
        Guard.Against(ArrivedAt is not null, "Stop has already been marked as arrived.");
        ArrivedAt = at;
    }

    public void SetDeparted(DateTime at)
    {
        Guard.Against(ArrivedAt is null, "Stop must be marked as arrived before departing.");
        Guard.Against(DepartedAt is not null, "Stop has already been marked as departed.");
        DepartedAt = at;
    }

    internal void UpdateSequenceOrder(int newOrder) => SequenceOrder = newOrder;
}
