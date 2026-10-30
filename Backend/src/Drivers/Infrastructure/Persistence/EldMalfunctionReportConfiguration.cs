using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Drivers.Domain.Hos;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>Maps the EldMalfunctionReport aggregate to drivers.eld_malfunction_reports (snake_case columns; status as its name).</summary>
public sealed class EldMalfunctionReportConfiguration : IEntityTypeConfiguration<EldMalfunctionReport>
{
    public void Configure(EntityTypeBuilder<EldMalfunctionReport> builder)
    {
        builder.ToTable("eld_malfunction_reports");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(r => r.TenantId).HasColumnName("tenant_id");
        builder.Property(r => r.DriverId).HasColumnName("driver_id");
        builder.Property(r => r.DriverName).HasColumnName("driver_name").HasMaxLength(128);
        builder.Property(r => r.VehicleId).HasColumnName("vehicle_id");
        builder.Property(r => r.Code).HasColumnName("code").HasMaxLength(EldMalfunctionReport.CodeMaxLength);
        builder.Property(r => r.NoticedAtUtc).HasColumnName("noticed_at_utc");
        builder.Property(r => r.ReportedAtUtc).HasColumnName("reported_at_utc");
        builder.Property(r => r.ReportedByUserId).HasColumnName("reported_by_user_id");
        builder.Property(r => r.Description).HasColumnName("description").HasMaxLength(EldMalfunctionReport.DescriptionMaxLength);
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32);
        builder.Property(r => r.ResolvedAtUtc).HasColumnName("resolved_at_utc");
        builder.Property(r => r.ResolvedByUserId).HasColumnName("resolved_by_user_id");
        builder.Property(r => r.ResolutionNote).HasColumnName("resolution_note").HasMaxLength(EldMalfunctionReport.DescriptionMaxLength);
        builder.Property(r => r.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.Ignore(r => r.IsOpen);

        builder.HasIndex(r => new { r.TenantId, r.DriverId, r.Status });
    }
}
