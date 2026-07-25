using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Drivers.Domain.Hos;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>Maps the HosLogEntry aggregate to drivers.hos_log_entries (snake_case columns).</summary>
public sealed class HosLogEntryConfiguration : IEntityTypeConfiguration<HosLogEntry>
{
    public void Configure(EntityTypeBuilder<HosLogEntry> builder)
    {
        builder.ToTable("hos_log_entries");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.DriverId).HasColumnName("driver_id");
        builder.Property(e => e.Date).HasColumnName("date");
        builder.Property(e => e.Duty).HasColumnName("duty").HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.OnDutyHours).HasColumnName("on_duty_hours");
        builder.Property(e => e.DrivingHours).HasColumnName("driving_hours");
        builder.Property(e => e.OffDutyHours).HasColumnName("off_duty_hours");
        builder.Property(e => e.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.EnteredBy).HasColumnName("entered_by").HasMaxLength(128);
        builder.Property(e => e.Note).HasColumnName("note").HasMaxLength(1000);
        builder.Property(e => e.RecordedAtUtc).HasColumnName("recorded_at_utc");

        builder.HasIndex(e => new { e.TenantId, e.DriverId });
    }
}
