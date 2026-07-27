using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>
/// Maps the VehicleInspection aggregate to fleet.vehicle_inspections (snake_case
/// columns). Checklist rows and defects are owned types mapped to jsonb. The unique
/// (tenant_id, manifest_id, type) index is the database backstop for the event
/// consumer's check-before-insert idempotency.
/// </summary>
public sealed class VehicleInspectionConfiguration : IEntityTypeConfiguration<VehicleInspection>
{
    public void Configure(EntityTypeBuilder<VehicleInspection> builder)
    {
        builder.ToTable("vehicle_inspections");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(i => i.TenantId).HasColumnName("tenant_id");

        builder.Property(i => i.VehicleId).HasColumnName("vehicle_id");

        builder.Property(i => i.Unit).HasColumnName("unit").HasMaxLength(32);

        builder.Property(i => i.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(i => i.DriverName).HasColumnName("driver_name").HasMaxLength(128);

        builder.Property(i => i.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(i => i.EnteredBy).HasColumnName("entered_by").HasMaxLength(128);
        builder.Property(i => i.TripNumber).HasColumnName("trip_number").HasMaxLength(32);
        builder.Property(i => i.ManifestId).HasColumnName("manifest_id");
        builder.Property(i => i.GeneratedWorkOrderId).HasColumnName("generated_work_order_id");
        builder.Property(i => i.PerformedAt).HasColumnName("performed_at");
        builder.Property(i => i.OdometerKm).HasColumnName("odometer_km");

        builder.Property(i => i.Result)
            .HasColumnName("result")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.OwnsMany(i => i.ChecklistItems, item => item.ToJson("checklist_items"));
        builder.OwnsMany(i => i.Defects, defect =>
        {
            defect.ToJson("defects");
            defect.Property(d => d.Severity).HasConversion<string>();
        });

        // Pre-trip sections (moved off the manifest) — multi-selects as text arrays, scalars as columns.
        builder.PrimitiveCollection(i => i.Weather)
            .HasColumnName("weather")
            .ElementType(e => e.HasConversion<string>());
        builder.Property(i => i.TemperatureC).HasColumnName("temperature_c").HasMaxLength(16);
        builder.PrimitiveCollection(i => i.RoadConditions)
            .HasColumnName("road_conditions")
            .ElementType(e => e.HasConversion<string>());
        builder.Property(i => i.Visibility)
            .HasColumnName("visibility")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(i => i.RoadAdvisories).HasColumnName("road_advisories").HasMaxLength(500);
        builder.Property(i => i.FuelLevel)
            .HasColumnName("fuel_level")
            .HasConversion<string>()
            .HasMaxLength(16);

        // Post-trip log & certification (moved off the manifest).
        builder.PrimitiveCollection(i => i.Issues).HasColumnName("issues");
        builder.PrimitiveCollection(i => i.Attestations).HasColumnName("attestations");
        builder.Property(i => i.DriverSignatureName).HasColumnName("driver_signature_name").HasMaxLength(128);
        builder.Property(i => i.CertifiedAt).HasColumnName("certified_at");
        builder.Property(i => i.FuelAdded).HasColumnName("fuel_added");
        builder.Property(i => i.FuelLitres).HasColumnName("fuel_litres").HasColumnType("numeric(8,2)");
        builder.Property(i => i.FuelCostCad).HasColumnName("fuel_cost_cad").HasColumnType("numeric(12,2)");

        builder.Property(i => i.CreatedAtUtc).HasColumnName("created_at_utc");

        builder.HasIndex(i => new { i.TenantId, i.ManifestId, i.Type }).IsUnique();
        builder.HasIndex(i => new { i.TenantId, i.Unit });

        // DomainEvents ignore + Version concurrency token come from ModuleDbContext's
        // central aggregate conventions.
    }
}
