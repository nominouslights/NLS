using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Fleet.Domain.Maintenance;

namespace NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a maintenance plan into <c>fleet.rm_maintenance_plans</c> —
/// a 1:1 mirror of the aggregate. <see cref="Items"/> and <see cref="Overhauls"/> are the
/// base table's jsonb verbatim, re-mapped with the same <c>OwnsMany(...).ToJson(...)</c>
/// shape (and the same enum-as-string conversions) as the aggregate.
/// </summary>
public sealed class MaintenancePlanReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = null!;
    public string VehicleModel { get; set; } = null!;
    public string ServiceClass { get; set; } = null!;
    public string? Notes { get; set; }
    public List<MaintenanceItem> Items { get; set; } = [];
    public List<OverhaulSpec> Overhauls { get; set; } = [];
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class MaintenancePlanReadModelConfiguration : IEntityTypeConfiguration<MaintenancePlanReadModel>
{
    public void Configure(EntityTypeBuilder<MaintenancePlanReadModel> builder)
    {
        builder.HasKey(p => p.Id);
        builder.ToTable("rm_maintenance_plans", FleetServiceCollectionExtensions.SchemaName);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.TenantId).HasColumnName("tenant_id");
        builder.Property(p => p.Name).HasColumnName("name");
        builder.Property(p => p.VehicleModel).HasColumnName("vehicle_model");
        builder.Property(p => p.ServiceClass).HasColumnName("service_class");
        builder.Property(p => p.Notes).HasColumnName("notes");
        builder.Property(p => p.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(p => p.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(p => p.Version).HasColumnName("version");

        // Shared with the maintenance_plans aggregate table — one jsonb mapping, no drift.
        MaintenancePlanDocumentMapping.MapItemsAndOverhauls(builder, p => p.Items, p => p.Overhauls);

        // Follows the write table's natural key, so name lookups on the read side are also
        // an index probe. Deliberately NOT unique — uniqueness lives on the write table
        // only; a unique index on an eventually-consistent projection can wedge the
        // projection worker when a delete+insert lands in the same batch.
        builder.HasIndex(p => new { p.TenantId, p.Name });
    }
}
