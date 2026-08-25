using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a plan assignment into <c>fleet.rm_pm_plan_assignments</c> —
/// a 1:1 mirror of the aggregate (pure vehicle-to-plan linkage).
/// </summary>
public sealed class PlanAssignmentReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid PlanId { get; set; }
    public DateTimeOffset AssignedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class PlanAssignmentReadModelConfiguration : IEntityTypeConfiguration<PlanAssignmentReadModel>
{
    public void Configure(EntityTypeBuilder<PlanAssignmentReadModel> builder)
    {
        builder.HasKey(a => a.Id);
        builder.ToTable("rm_pm_plan_assignments", FleetServiceCollectionExtensions.SchemaName);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.TenantId).HasColumnName("tenant_id");
        builder.Property(a => a.VehicleId).HasColumnName("vehicle_id");
        builder.Property(a => a.PlanId).HasColumnName("plan_id");
        builder.Property(a => a.AssignedAtUtc).HasColumnName("assigned_at_utc");
        builder.Property(a => a.Version).HasColumnName("version");

        // Reads happen here, not on the write table: the vehicle lookup mirrors the write
        // table's one-plan-per-vehicle key, and (tenant_id, plan_id) serves "which vehicles
        // follow this plan".
        builder.HasIndex(a => new { a.TenantId, a.VehicleId }).IsUnique();
        builder.HasIndex(a => new { a.TenantId, a.PlanId });
    }
}
