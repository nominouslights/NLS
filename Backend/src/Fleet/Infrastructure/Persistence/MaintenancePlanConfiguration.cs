using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Fleet.Domain.Maintenance;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>
/// Maps the MaintenancePlan aggregate to fleet.maintenance_plans. Items and Overhauls are
/// owned collections mapped to jsonb — codes inside the payload are the natural keys, so the
/// whole document is replaced on update (no per-line identity round-tripping).
/// </summary>
public sealed class MaintenancePlanConfiguration : IEntityTypeConfiguration<MaintenancePlan>
{
    public void Configure(EntityTypeBuilder<MaintenancePlan> builder)
    {
        builder.ToTable("maintenance_plans");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(p => p.TenantId).HasColumnName("tenant_id");
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(MaintenancePlan.NameMaxLength);
        builder.Property(p => p.VehicleModel).HasColumnName("vehicle_model").HasMaxLength(MaintenancePlan.VehicleModelMaxLength);
        builder.Property(p => p.ServiceClass).HasColumnName("service_class").HasMaxLength(MaintenancePlan.ServiceClassMaxLength);
        builder.Property(p => p.Notes).HasColumnName("notes").HasMaxLength(MaintenancePlan.NotesMaxLength);
        builder.Property(p => p.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(p => p.UpdatedAtUtc).HasColumnName("updated_at_utc");

        // Shared with the rm_maintenance_plans mirror — one jsonb mapping, no drift.
        MaintenancePlanDocumentMapping.MapItemsAndOverhauls(builder, p => p.Items, p => p.Overhauls);

        builder.HasIndex(p => new { p.TenantId, p.Name }).IsUnique();
    }
}
