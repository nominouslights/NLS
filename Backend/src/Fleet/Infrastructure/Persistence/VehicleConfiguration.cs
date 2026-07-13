using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Fleet.Domain.Vehicles;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>Maps the Vehicle aggregate to fleet.vehicles (snake_case columns).</summary>
public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(v => v.TenantId).HasColumnName("tenant_id");

        builder.Property(v => v.UnitNumber)
            .HasColumnName("unit_number")
            .HasMaxLength(32);

        builder.Property(v => v.Vin)
            .HasColumnName("vin")
            .HasConversion(vin => vin.Value, value => Vin.Create(value).Value)
            .HasColumnType("varchar(17)");

        builder.Property(v => v.Make).HasColumnName("make").HasMaxLength(64);
        builder.Property(v => v.Model).HasColumnName("model").HasMaxLength(64);
        builder.Property(v => v.Year).HasColumnName("year");
        builder.Property(v => v.SeatingCapacity).HasColumnName("seating_capacity");
        builder.Property(v => v.LicencePlate).HasColumnName("licence_plate").HasMaxLength(32);
        builder.Property(v => v.RequiredLicenceClass).HasColumnName("required_licence_class").HasMaxLength(32);

        builder.Property(v => v.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(v => v.StatusReason).HasColumnName("status_reason").HasMaxLength(500);
        builder.Property(v => v.OdometerKm).HasColumnName("odometer_km");
        builder.Property(v => v.AcquisitionCostCad).HasColumnName("acquisition_cost_cad").HasColumnType("numeric(12,2)");
        builder.Property(v => v.EndOfLifeKm).HasColumnName("end_of_life_km");
        builder.Property(v => v.SalePriceCad).HasColumnName("sale_price_cad").HasColumnType("numeric(12,2)");
        builder.Property(v => v.DisposedAtUtc).HasColumnName("disposed_at_utc");
        builder.Property(v => v.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(v => v.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(v => new { v.TenantId, v.UnitNumber }).IsUnique();
        builder.HasIndex(v => new { v.TenantId, v.Vin }).IsUnique();

        // DomainEvents ignore + Version concurrency token come from ModuleDbContext's
        // central aggregate conventions.
    }
}
