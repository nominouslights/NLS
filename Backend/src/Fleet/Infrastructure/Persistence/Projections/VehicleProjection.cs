using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;

namespace NorthernLink.Fleet.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="Vehicle"/> into <c>fleet.rm_vehicles</c>. The depreciation fields are read
/// straight off the aggregate's computed properties, so the API and the read model can never
/// disagree about a rounded value.
/// </summary>
internal sealed class VehicleProjection : FleetProjection<Vehicle, VehicleReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(Vehicle));

    protected override void Map(Vehicle source, VehicleReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.UnitNumber = source.UnitNumber;
        row.Vin = source.Vin.Value;
        row.Make = source.Make;
        row.Model = source.Model;
        row.Year = source.Year;
        row.SeatingCapacity = source.SeatingCapacity;
        row.LicencePlate = source.LicencePlate;
        row.RequiredLicenceClass = source.RequiredLicenceClass;
        row.Status = source.Status.ToString();
        row.StatusReason = source.StatusReason;
        row.OdometerKm = source.OdometerKm;
        row.AcquisitionCostCad = source.AcquisitionCostCad;
        row.EndOfLifeKm = source.EndOfLifeKm;
        row.SalePriceCad = source.SalePriceCad;
        row.DisposedAtUtc = source.DisposedAtUtc;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;

        // Derived — the aggregate is the only implementation.
        row.RequiresPeriodicInspection = source.RequiresPeriodicInspection;
        row.CurrentValueCad = source.CurrentValueCad;
        row.RemainingKm = source.RemainingKm;
        row.LifeUsedPct = source.LifeUsedPct;
    }
}
