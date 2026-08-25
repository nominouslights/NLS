using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Fleet.Domain.Maintenance;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>
/// Maps the PlanAssignment aggregate to fleet.pm_plan_assignments. One plan per vehicle —
/// enforced by the unique (tenant_id, vehicle_id) index.
/// </summary>
public sealed class PlanAssignmentConfiguration : IEntityTypeConfiguration<PlanAssignment>
{
    public void Configure(EntityTypeBuilder<PlanAssignment> builder)
    {
        builder.ToTable("pm_plan_assignments");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(a => a.TenantId).HasColumnName("tenant_id");
        builder.Property(a => a.VehicleId).HasColumnName("vehicle_id");
        builder.Property(a => a.PlanId).HasColumnName("plan_id");
        builder.Property(a => a.AssignedAtUtc).HasColumnName("assigned_at_utc");

        // Only the uniqueness guard lives on the write table; the (tenant_id, plan_id)
        // listing path is served by rm_pm_plan_assignments.
        builder.HasIndex(a => new { a.TenantId, a.VehicleId }).IsUnique();
    }
}
