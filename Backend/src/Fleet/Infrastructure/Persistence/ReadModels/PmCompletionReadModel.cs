using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a PM completion into <c>fleet.rm_pm_completions</c> — a 1:1
/// mirror of the append-only aggregate. The read service folds these to the latest row per
/// item code, which is the input to every due computation.
/// </summary>
public sealed class PmCompletionReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid PlanId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string Kind { get; set; } = null!;
    public DateOnly PerformedAt { get; set; }
    public int OdometerKm { get; set; }
    public string PerformedBy { get; set; } = null!;
    public Guid? WorkOrderId { get; set; }
    public string? Measurement { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class PmCompletionReadModelConfiguration : IEntityTypeConfiguration<PmCompletionReadModel>
{
    public void Configure(EntityTypeBuilder<PmCompletionReadModel> builder)
    {
        builder.HasKey(c => c.Id);
        builder.ToTable("rm_pm_completions", FleetServiceCollectionExtensions.SchemaName);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.VehicleId).HasColumnName("vehicle_id");
        builder.Property(c => c.PlanId).HasColumnName("plan_id");
        builder.Property(c => c.ItemCode).HasColumnName("item_code");
        builder.Property(c => c.Kind).HasColumnName("kind");
        builder.Property(c => c.PerformedAt).HasColumnName("performed_at");
        builder.Property(c => c.OdometerKm).HasColumnName("odometer_km");
        builder.Property(c => c.PerformedBy).HasColumnName("performed_by");
        builder.Property(c => c.WorkOrderId).HasColumnName("work_order_id");
        builder.Property(c => c.Measurement).HasColumnName("measurement");
        builder.Property(c => c.Notes).HasColumnName("notes");
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(c => c.Version).HasColumnName("version");

        // Same "latest completion per item code" access path as the write table.
        builder.HasIndex(c => new { c.TenantId, c.VehicleId, c.ItemCode, c.PerformedAt })
            .IsDescending(false, false, false, true);
    }
}
