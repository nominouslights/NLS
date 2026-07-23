using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Fleet.Domain.Services;

namespace NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a service record, via <c>fleet.mv_service_records</c> /
/// <c>fleet.v_service_records</c>. <see cref="ItemsChanged"/> is the base table's text[]
/// verbatim; <see cref="PartsUsed"/> is its parts_used jsonb verbatim, re-mapped with the
/// same <c>OwnsMany(...).ToJson(...)</c> shape as the aggregate. Because a keyless entity
/// can't own a jsonb collection, this read model is KEYED (on <see cref="Id"/>) — a keyed
/// entity mapped to a view is still read-only.
/// </summary>
public sealed class ServiceRecordReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid VehicleId { get; set; }
    public string Number { get; set; } = null!;
    public DateTimeOffset Date { get; set; }
    public string PerformedBy { get; set; } = null!;
    public string Category { get; set; } = null!;
    public int OdometerKm { get; set; }
    public List<string> ItemsChanged { get; set; } = [];
    public string Reason { get; set; } = null!;
    public List<ServicePart> PartsUsed { get; set; } = [];
    public decimal? LaborHours { get; set; }
    public decimal? CostCad { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class ServiceRecordReadModelConfiguration : IEntityTypeConfiguration<ServiceRecordReadModel>
{
    public void Configure(EntityTypeBuilder<ServiceRecordReadModel> builder)
    {
        builder.HasKey(s => s.Id);
        builder.ToTable("rm_service_records", FleetServiceCollectionExtensions.SchemaName);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.TenantId).HasColumnName("tenant_id");
        builder.Property(s => s.VehicleId).HasColumnName("vehicle_id");
        builder.Property(s => s.Number).HasColumnName("number");
        builder.Property(s => s.Date).HasColumnName("date");
        builder.Property(s => s.PerformedBy).HasColumnName("performed_by");
        builder.Property(s => s.Category).HasColumnName("category");
        builder.Property(s => s.OdometerKm).HasColumnName("odometer_km");
        builder.Property(s => s.ItemsChanged).HasColumnName("items_changed").HasColumnType("text[]");
        builder.Property(s => s.Reason).HasColumnName("reason");
        builder.Property(s => s.LaborHours).HasColumnName("labor_hours");
        builder.Property(s => s.CostCad).HasColumnName("cost_cad");
        builder.Property(s => s.WorkOrderId).HasColumnName("work_order_id");
        builder.Property(s => s.Notes).HasColumnName("notes");
        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(s => s.Version).HasColumnName("version");

        builder.OwnsMany(s => s.PartsUsed, part => part.ToJson("parts_used"));
    }
}
