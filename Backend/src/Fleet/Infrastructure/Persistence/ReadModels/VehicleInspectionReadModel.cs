using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a vehicle inspection into <c>fleet.rm_vehicle_inspections</c>. The
/// checklist and defects jsonb are carried verbatim and re-mapped with the same
/// <c>OwnsMany(...).ToJson(...)</c> shape as the aggregate, so this read model is KEYED (on
/// <see cref="Id"/>) — a keyless entity can't own a jsonb collection. The pre/post section
/// multi-selects are text arrays, matching the write side.
/// </summary>
public sealed class VehicleInspectionReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? VehicleId { get; set; }
    public string Unit { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string DriverName { get; set; } = null!;
    public string Source { get; set; } = null!;
    public string? EnteredBy { get; set; }
    public string? TripNumber { get; set; }
    public Guid? ManifestId { get; set; }
    public Guid? GeneratedWorkOrderId { get; set; }
    public DateTimeOffset PerformedAt { get; set; }
    public int? OdometerKm { get; set; }
    public string Result { get; set; } = null!;
    public List<InspectionChecklistItem> ChecklistItems { get; set; } = [];
    public List<InspectionDefect> Defects { get; set; } = [];
    public List<InspectionWeather> Weather { get; set; } = [];
    public string? TemperatureC { get; set; }
    public List<InspectionRoadCondition> RoadConditions { get; set; } = [];
    public InspectionVisibility? Visibility { get; set; }
    public string? RoadAdvisories { get; set; }
    public InspectionFuelLevel? FuelLevel { get; set; }
    public List<string> Issues { get; set; } = [];
    public List<bool> Attestations { get; set; } = [];
    public string? DriverSignatureName { get; set; }
    public DateTimeOffset? CertifiedAt { get; set; }
    public bool FuelAdded { get; set; }
    public decimal? FuelLitres { get; set; }
    public decimal? FuelCostCad { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class VehicleInspectionReadModelConfiguration : IEntityTypeConfiguration<VehicleInspectionReadModel>
{
    public void Configure(EntityTypeBuilder<VehicleInspectionReadModel> builder)
    {
        builder.HasKey(i => i.Id);
        builder.ToTable("rm_vehicle_inspections", FleetServiceCollectionExtensions.SchemaName);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.TenantId).HasColumnName("tenant_id");
        builder.Property(i => i.VehicleId).HasColumnName("vehicle_id");
        builder.Property(i => i.Unit).HasColumnName("unit");
        builder.Property(i => i.Type).HasColumnName("type");
        builder.Property(i => i.DriverName).HasColumnName("driver_name");
        builder.Property(i => i.Source).HasColumnName("source");
        builder.Property(i => i.EnteredBy).HasColumnName("entered_by");
        builder.Property(i => i.TripNumber).HasColumnName("trip_number");
        builder.Property(i => i.ManifestId).HasColumnName("manifest_id");
        builder.Property(i => i.GeneratedWorkOrderId).HasColumnName("generated_work_order_id");
        builder.Property(i => i.PerformedAt).HasColumnName("performed_at");
        builder.Property(i => i.OdometerKm).HasColumnName("odometer_km");
        builder.Property(i => i.Result).HasColumnName("result");
        builder.Property(i => i.TemperatureC).HasColumnName("temperature_c");
        builder.Property(i => i.Visibility).HasColumnName("visibility").HasConversion<string>();
        builder.Property(i => i.RoadAdvisories).HasColumnName("road_advisories");
        builder.Property(i => i.FuelLevel).HasColumnName("fuel_level").HasConversion<string>();
        builder.Property(i => i.DriverSignatureName).HasColumnName("driver_signature_name");
        builder.Property(i => i.CertifiedAt).HasColumnName("certified_at");
        builder.Property(i => i.FuelAdded).HasColumnName("fuel_added");
        builder.Property(i => i.FuelLitres).HasColumnName("fuel_litres");
        builder.Property(i => i.FuelCostCad).HasColumnName("fuel_cost_cad");
        builder.Property(i => i.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(i => i.Version).HasColumnName("version");

        builder.PrimitiveCollection(i => i.Weather)
            .HasColumnName("weather")
            .ElementType(e => e.HasConversion<string>());
        builder.PrimitiveCollection(i => i.RoadConditions)
            .HasColumnName("road_conditions")
            .ElementType(e => e.HasConversion<string>());
        builder.PrimitiveCollection(i => i.Issues).HasColumnName("issues");
        builder.PrimitiveCollection(i => i.Attestations).HasColumnName("attestations");

        builder.OwnsMany(i => i.ChecklistItems, item => item.ToJson("checklist_items"));
        builder.OwnsMany(i => i.Defects, defect =>
        {
            defect.ToJson("defects");
            defect.Property(d => d.Severity).HasConversion<string>();
        });
    }
}
