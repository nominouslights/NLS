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

        builder.OwnsMany(p => p.Items, item =>
        {
            item.ToJson("items");

            // Explicit, because EF serializes an enum inside owned JSON as an integer by
            // default (same reasoning as TripManifest.Passengers): a jsonb payload gets read
            // by people and hand-written SQL, and an opaque 0/1/2 in there is a trap.
            item.Property(i => i.Tier).HasConversion<string>();
            item.Property(i => i.Task).HasConversion<string>();
        });

        builder.OwnsMany(p => p.Overhauls, overhaul =>
        {
            overhaul.ToJson("overhauls");
            overhaul.Property(o => o.LabourHours).HasPrecision(8, 2);
            overhaul.Property(o => o.PartsCad).HasPrecision(12, 2);
        });

        builder.HasIndex(p => new { p.TenantId, p.Name }).IsUnique();
    }
}
