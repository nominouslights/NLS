using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Trips;

public sealed class TripCargoItem : Entity<Guid>
{
    public Guid TripId { get; private set; }
    public CargoType CargoType { get; private set; }
    public string? Description { get; private set; }
    public int Quantity { get; private set; }

    private TripCargoItem() { }

    public static TripCargoItem Create(
        Guid tripId,
        CargoType cargoType,
        string? description,
        int quantity)
    {
        Guard.Against(quantity < 1, "Quantity must be at least 1.");

        return new TripCargoItem
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            CargoType = cargoType,
            Description = description,
            Quantity = quantity
        };
    }
}
