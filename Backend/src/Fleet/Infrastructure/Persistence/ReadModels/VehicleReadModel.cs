using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a vehicle, materialized by <c>fleet.mv_vehicles</c> and read
/// through the tenant wrapper view <c>fleet.v_vehicles</c>. The depreciation fields
/// (<see cref="CurrentValueCad"/>, <see cref="RemainingKm"/>, <see cref="LifeUsedPct"/>,
/// <see cref="RequiresPeriodicInspection"/>) are computed in SQL by the matview — no C#
/// computed properties on the read path — using <c>fleet.round_even</c> so the values match
/// <c>Vehicle</c>'s <c>Math.Round</c> (banker's rounding) exactly. <see cref="Version"/> is
/// the aggregate's optimistic-concurrency version captured at last refresh; staleness is
/// <c>event_journal.aggregate_version &gt; version</c>.
/// </summary>
public sealed class VehicleReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string UnitNumber { get; set; } = null!;
    public string Vin { get; set; } = null!;
    public string Make { get; set; } = null!;
    public string Model { get; set; } = null!;
    public int Year { get; set; }
    public int SeatingCapacity { get; set; }
    public string LicencePlate { get; set; } = null!;
    public string RequiredLicenceClass { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? StatusReason { get; set; }
    public int OdometerKm { get; set; }
    public decimal AcquisitionCostCad { get; set; }
    public int EndOfLifeKm { get; set; }
    public decimal? SalePriceCad { get; set; }
    public DateTimeOffset? DisposedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }

    // Derived columns, computed by the matview.
    public bool RequiresPeriodicInspection { get; set; }
    public decimal CurrentValueCad { get; set; }
    public int RemainingKm { get; set; }
    public decimal LifeUsedPct { get; set; }
}

/// <summary>
/// Maps <see cref="VehicleReadModel"/> to the tenant wrapper view fleet.v_vehicles
/// (read-only, keyless). The tenant query filter is applied centrally in
/// <c>FleetDbContext.ConfigureModule</c> so it references the context's per-instance
/// TenantId, exactly like the aggregate filters.
/// </summary>
public sealed class VehicleReadModelConfiguration : IEntityTypeConfiguration<VehicleReadModel>
{
    public void Configure(EntityTypeBuilder<VehicleReadModel> builder)
    {
        builder.HasKey(v => v.Id);
        builder.ToTable("rm_vehicles", FleetServiceCollectionExtensions.SchemaName);

        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.TenantId).HasColumnName("tenant_id");
        builder.Property(v => v.UnitNumber).HasColumnName("unit_number");
        builder.Property(v => v.Vin).HasColumnName("vin");
        builder.Property(v => v.Make).HasColumnName("make");
        builder.Property(v => v.Model).HasColumnName("model");
        builder.Property(v => v.Year).HasColumnName("year");
        builder.Property(v => v.SeatingCapacity).HasColumnName("seating_capacity");
        builder.Property(v => v.LicencePlate).HasColumnName("licence_plate");
        builder.Property(v => v.RequiredLicenceClass).HasColumnName("required_licence_class");
        builder.Property(v => v.Status).HasColumnName("status");
        builder.Property(v => v.StatusReason).HasColumnName("status_reason");
        builder.Property(v => v.OdometerKm).HasColumnName("odometer_km");
        builder.Property(v => v.AcquisitionCostCad).HasColumnName("acquisition_cost_cad");
        builder.Property(v => v.EndOfLifeKm).HasColumnName("end_of_life_km");
        builder.Property(v => v.SalePriceCad).HasColumnName("sale_price_cad");
        builder.Property(v => v.DisposedAtUtc).HasColumnName("disposed_at_utc");
        builder.Property(v => v.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(v => v.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(v => v.Version).HasColumnName("version");
        builder.Property(v => v.RequiresPeriodicInspection).HasColumnName("requires_periodic_inspection");
        builder.Property(v => v.CurrentValueCad).HasColumnName("current_value_cad");
        builder.Property(v => v.RemainingKm).HasColumnName("remaining_km");
        builder.Property(v => v.LifeUsedPct).HasColumnName("life_used_pct");
    }
}
