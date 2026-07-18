using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Fleet.Domain.Services;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>
/// Maps the ServiceRecord aggregate to fleet.service_records. ItemsChanged is a Postgres
/// text[] column; PartsUsed is an owned collection mapped to jsonb.
/// </summary>
public sealed class ServiceRecordConfiguration : IEntityTypeConfiguration<ServiceRecord>
{
    public void Configure(EntityTypeBuilder<ServiceRecord> builder)
    {
        builder.ToTable("service_records");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(s => s.TenantId).HasColumnName("tenant_id");
        builder.Property(s => s.VehicleId).HasColumnName("vehicle_id");
        builder.Property(s => s.Number).HasColumnName("number").HasMaxLength(16);
        builder.Property(s => s.Date).HasColumnName("date");
        builder.Property(s => s.PerformedBy).HasColumnName("performed_by").HasMaxLength(200);

        builder.Property(s => s.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(s => s.OdometerKm).HasColumnName("odometer_km");
        builder.Property(s => s.ItemsChanged).HasColumnName("items_changed").HasColumnType("text[]");
        builder.Property(s => s.Reason).HasColumnName("reason").HasMaxLength(2000);
        builder.Property(s => s.LaborHours).HasColumnName("labor_hours").HasColumnType("numeric(8,2)");
        builder.Property(s => s.CostCad).HasColumnName("cost_cad").HasColumnType("numeric(12,2)");
        builder.Property(s => s.WorkOrderId).HasColumnName("work_order_id");
        builder.Property(s => s.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc");

        builder.OwnsMany(s => s.PartsUsed, part => part.ToJson("parts_used"));

        builder.HasIndex(s => new { s.TenantId, s.Number }).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.VehicleId });
    }
}
