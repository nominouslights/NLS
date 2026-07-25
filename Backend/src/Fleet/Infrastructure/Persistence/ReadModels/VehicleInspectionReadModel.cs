using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a vehicle inspection, via <c>fleet.mv_vehicle_inspections</c> /
/// <c>fleet.v_vehicle_inspections</c>. The checklist and defects jsonb are carried verbatim
/// and re-mapped with the same <c>OwnsMany(...).ToJson(...)</c> shape as the aggregate, so
/// this read model is KEYED (on <see cref="Id"/>) — a keyless entity can't own a jsonb
/// collection.
/// </summary>
public sealed class VehicleInspectionReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
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
        builder.Property(i => i.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(i => i.Version).HasColumnName("version");

        builder.OwnsMany(i => i.ChecklistItems, item => item.ToJson("checklist_items"));
        builder.OwnsMany(i => i.Defects, defect =>
        {
            defect.ToJson("defects");
            defect.Property(d => d.Severity).HasConversion<string>();
        });
    }
}
