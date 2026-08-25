using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Fleet.Domain.Maintenance;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>
/// Maps the PmCompletion aggregate to fleet.pm_completions. Append-only rows with no query
/// indexes: every "latest completion per item code" read goes through rm_pm_completions,
/// which carries the descending composite index for that lookup.
/// </summary>
public sealed class PmCompletionConfiguration : IEntityTypeConfiguration<PmCompletion>
{
    public void Configure(EntityTypeBuilder<PmCompletion> builder)
    {
        builder.ToTable("pm_completions");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.VehicleId).HasColumnName("vehicle_id");
        builder.Property(c => c.PlanId).HasColumnName("plan_id");
        builder.Property(c => c.ItemCode).HasColumnName("item_code").HasMaxLength(MaintenancePlan.CodeMaxLength);

        builder.Property(c => c.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(16);

        // DateOnly maps to Postgres `date` natively under Npgsql.
        builder.Property(c => c.PerformedAt).HasColumnName("performed_at");
        builder.Property(c => c.OdometerKm).HasColumnName("odometer_km");
        builder.Property(c => c.PerformedBy).HasColumnName("performed_by").HasMaxLength(PmCompletion.PerformedByMaxLength);
        builder.Property(c => c.WorkOrderId).HasColumnName("work_order_id");
        builder.Property(c => c.Measurement).HasColumnName("measurement").HasMaxLength(PmCompletion.MeasurementMaxLength);
        builder.Property(c => c.Notes).HasColumnName("notes").HasMaxLength(PmCompletion.NotesMaxLength);
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc");
    }
}
