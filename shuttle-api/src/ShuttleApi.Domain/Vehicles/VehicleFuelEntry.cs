using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Vehicles;

public sealed class VehicleFuelEntry : Entity<Guid>
{
    public Guid VehicleId { get; private set; }
    public DateTime FuelledAt { get; private set; }
    public decimal FuelLitres { get; private set; }
    public decimal TotalCostDollars { get; private set; }
    public int? OdometerAtFuelling { get; private set; }
    public string? ReceiptPhotoUrl { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private VehicleFuelEntry() { }

    public static VehicleFuelEntry Create(
        Guid vehicleId,
        DateTime fuelledAt,
        decimal fuelLitres,
        decimal totalCostDollars,
        int? odometerAtFuelling,
        string? receiptPhotoUrl,
        string? notes)
    {
        Guard.Against(fuelLitres <= 0, "Fuel litres must be greater than zero.");
        Guard.Against(totalCostDollars < 0, "Total cost cannot be negative.");
        Guard.Against(odometerAtFuelling.HasValue && odometerAtFuelling.Value < 0, "Odometer reading cannot be negative.");

        return new VehicleFuelEntry
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            FuelledAt = fuelledAt,
            FuelLitres = fuelLitres,
            TotalCostDollars = totalCostDollars,
            OdometerAtFuelling = odometerAtFuelling,
            ReceiptPhotoUrl = receiptPhotoUrl,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };
    }
}
