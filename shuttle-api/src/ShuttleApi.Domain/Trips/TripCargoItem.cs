using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Trips;

public sealed class TripCargoItem : Entity<Guid>
{
    public Guid TripId { get; private set; }
    public CargoType CargoType { get; private set; }
    public string? Description { get; private set; }
    public int Quantity { get; private set; }
    public decimal? WeightKg { get; private set; }
    public decimal? Charge { get; private set; }
    public bool IsHazmat { get; private set; }
    public bool IsSecured { get; private set; }

    private TripCargoItem() { }

    public static TripCargoItem Create(
        Guid tripId,
        CargoType cargoType,
        string? description,
        int quantity,
        decimal? weightKg = null,
        decimal? charge = null,
        bool isHazmat = false,
        bool isSecured = false)
    {
        Guard.Against(quantity < 1, "Quantity must be at least 1.");
        Guard.Against(weightKg.HasValue && weightKg.Value <= 0, "Weight must be greater than zero.");

        return new TripCargoItem
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            CargoType = cargoType,
            Description = description,
            Quantity = quantity,
            WeightKg = weightKg,
            Charge = charge,
            IsHazmat = isHazmat,
            IsSecured = isSecured
        };
    }
}
