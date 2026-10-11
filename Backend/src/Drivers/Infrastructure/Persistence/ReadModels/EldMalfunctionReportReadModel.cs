using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Drivers.Infrastructure.Persistence.ReadModels;

/// <summary>Read-side projection of an <c>EldMalfunctionReport</c> into <c>drivers.rm_eld_malfunction_reports</c>, maintained by <c>EldMalfunctionReportProjection</c>.</summary>
public sealed class EldMalfunctionReportReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DriverId { get; set; }
    public string DriverName { get; set; } = null!;
    public Guid? VehicleId { get; set; }
    public string Code { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTimeOffset NoticedAtUtc { get; set; }
    public DateTimeOffset ReportedAtUtc { get; set; }
    public Guid ReportedByUserId { get; set; }
    public string Description { get; set; } = null!;
    public DateTimeOffset? ResolvedAtUtc { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolutionNote { get; set; }
    public int Version { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class EldMalfunctionReportReadModelConfiguration : IEntityTypeConfiguration<EldMalfunctionReportReadModel>
{
    public void Configure(EntityTypeBuilder<EldMalfunctionReportReadModel> builder)
    {
        builder.HasKey(r => r.Id);
        builder.ToTable("rm_eld_malfunction_reports", DriversServiceCollectionExtensions.SchemaName);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id");
        builder.Property(r => r.DriverId).HasColumnName("driver_id");
        builder.Property(r => r.DriverName).HasColumnName("driver_name");
        builder.Property(r => r.VehicleId).HasColumnName("vehicle_id");
        builder.Property(r => r.Code).HasColumnName("code").HasMaxLength(32);
        builder.Property(r => r.Status).HasColumnName("status").HasMaxLength(32);
        builder.Property(r => r.NoticedAtUtc).HasColumnName("noticed_at_utc");
        builder.Property(r => r.ReportedAtUtc).HasColumnName("reported_at_utc");
        builder.Property(r => r.ReportedByUserId).HasColumnName("reported_by_user_id");
        builder.Property(r => r.Description).HasColumnName("description");
        builder.Property(r => r.ResolvedAtUtc).HasColumnName("resolved_at_utc");
        builder.Property(r => r.ResolvedByUserId).HasColumnName("resolved_by_user_id");
        builder.Property(r => r.ResolutionNote).HasColumnName("resolution_note");
        builder.Property(r => r.Version).HasColumnName("version");
        builder.Property(r => r.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(r => new { r.TenantId, r.DriverId, r.Status });
    }
}
