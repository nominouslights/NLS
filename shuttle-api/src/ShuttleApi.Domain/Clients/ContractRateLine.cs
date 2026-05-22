using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Clients;

public sealed class ContractRateLine : Entity<Guid>
{
    public Guid ContractId { get; private set; }
    public string BillingCode { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string VehicleType { get; private set; } = string.Empty;
    public int? MaxDistanceKm { get; private set; }
    public bool CargoIncluded { get; private set; }
    public decimal DayRate { get; private set; }

    private ContractRateLine() { }

    public static ContractRateLine Create(
        Guid contractId,
        string billingCode,
        string description,
        string vehicleType,
        int? maxDistanceKm,
        bool cargoIncluded,
        decimal dayRate)
    {
        return new ContractRateLine
        {
            Id = Guid.NewGuid(),
            ContractId = contractId,
            BillingCode = billingCode,
            Description = description,
            VehicleType = vehicleType,
            MaxDistanceKm = maxDistanceKm,
            CargoIncluded = cargoIncluded,
            DayRate = dayRate
        };
    }
}
