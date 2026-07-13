using NorthernLink.Fleet.Application.Vehicles;
using NorthernLink.Fleet.Domain.Vehicles;

namespace NorthernLink.Fleet.Application.Vehicles;

/// <summary>Maps domain entities to the module's public response contracts.</summary>
public static class VehicleResponseMapper
{
    public static VehicleResponse ToResponse(Vehicle vehicle) => new(
        vehicle.Id,
        vehicle.UnitNumber,
        vehicle.Vin.Value,
        vehicle.Make,
        vehicle.Model,
        vehicle.Year,
        vehicle.SeatingCapacity,
        vehicle.LicencePlate,
        vehicle.RequiredLicenceClass,
        vehicle.Status.ToString(),
        vehicle.StatusReason,
        vehicle.OdometerKm,
        vehicle.AcquisitionCostCad,
        vehicle.EndOfLifeKm,
        vehicle.CurrentValueCad,
        vehicle.RemainingKm,
        vehicle.LifeUsedPct,
        vehicle.RequiresPeriodicInspection,
        vehicle.SalePriceCad,
        vehicle.DisposedAtUtc,
        vehicle.CreatedAtUtc,
        vehicle.UpdatedAtUtc);

    public static RetirementCertificateResponse ToResponse(RetirementCertificate certificate) => new(
        certificate.Id,
        certificate.VehicleId,
        certificate.CertificateNumber,
        certificate.Vin,
        certificate.UnitNumber,
        certificate.Make,
        certificate.Model,
        certificate.Year,
        certificate.FinalOdometerKm,
        certificate.RetirementReason,
        certificate.RetiredAtUtc,
        certificate.IssuedAtUtc);
}
