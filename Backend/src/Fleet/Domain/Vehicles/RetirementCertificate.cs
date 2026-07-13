using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Vehicles;

/// <summary>
/// Durable record issued on every transition into <see cref="VehicleStatus.Retired"/>
/// (manual or auto at end of life). Snapshots the vehicle's identifying details so the
/// certificate stays meaningful after later edits or disposal. Numbered
/// <c>RC-{year}-{sequence}</c> per tenant.
/// </summary>
public sealed class RetirementCertificate : Entity, ITenantScoped
{
    private RetirementCertificate()
    {
        // EF Core materialization only.
        CertificateNumber = null!;
        Vin = null!;
        UnitNumber = null!;
        Make = null!;
        Model = null!;
        RetirementReason = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid VehicleId { get; private set; }
    public string CertificateNumber { get; private set; }
    public string Vin { get; private set; }
    public string UnitNumber { get; private set; }
    public string Make { get; private set; }
    public string Model { get; private set; }
    public int Year { get; private set; }
    public int FinalOdometerKm { get; private set; }
    public string RetirementReason { get; private set; }
    public DateTimeOffset RetiredAtUtc { get; private set; }
    public DateTimeOffset IssuedAtUtc { get; private set; }

    /// <summary>Issues a certificate for a vehicle that has just transitioned into Retired.</summary>
    public static RetirementCertificate Issue(Vehicle vehicle, int sequence, DateTimeOffset utcNow) =>
        new()
        {
            TenantId = vehicle.TenantId,
            VehicleId = vehicle.Id,
            CertificateNumber = $"RC-{utcNow.Year}-{sequence:D4}",
            Vin = vehicle.Vin.Value,
            UnitNumber = vehicle.UnitNumber,
            Make = vehicle.Make,
            Model = vehicle.Model,
            Year = vehicle.Year,
            FinalOdometerKm = vehicle.OdometerKm,
            RetirementReason = vehicle.StatusReason ?? "Retired",
            RetiredAtUtc = utcNow,
            IssuedAtUtc = utcNow,
        };
}
