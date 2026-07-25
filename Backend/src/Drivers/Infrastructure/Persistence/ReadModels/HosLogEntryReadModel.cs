using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Drivers.Domain.Hos;

namespace NorthernLink.Drivers.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of an HOS log entry into <c>drivers.rm_hos_log_entries</c>.
/// <see cref="Duty"/> and <see cref="Source"/> are stored as the clean enums (persisted as
/// strings); the friendly display strings are produced at the response boundary by
/// <c>HosDisplay</c>. The CVDHS remaining/violation gauge is derived by the frontend from
/// the raw hours, never stored.
/// </summary>
public sealed class HosLogEntryReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DriverId { get; set; }
    public DateOnly Date { get; set; }
    public DutyStatus Duty { get; set; }
    public decimal OnDutyHours { get; set; }
    public decimal DrivingHours { get; set; }
    public decimal OffDutyHours { get; set; }
    public HosLogEntrySource Source { get; set; }
    public string? EnteredBy { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class HosLogEntryReadModelConfiguration : IEntityTypeConfiguration<HosLogEntryReadModel>
{
    public void Configure(EntityTypeBuilder<HosLogEntryReadModel> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable("rm_hos_log_entries", DriversServiceCollectionExtensions.SchemaName);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.DriverId).HasColumnName("driver_id");
        builder.Property(e => e.Date).HasColumnName("date");
        builder.Property(e => e.Duty).HasColumnName("duty").HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.OnDutyHours).HasColumnName("on_duty_hours");
        builder.Property(e => e.DrivingHours).HasColumnName("driving_hours");
        builder.Property(e => e.OffDutyHours).HasColumnName("off_duty_hours");
        builder.Property(e => e.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.EnteredBy).HasColumnName("entered_by");
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.RecordedAtUtc).HasColumnName("recorded_at_utc");
        builder.Property(e => e.Version).HasColumnName("version");

        builder.HasIndex(e => new { e.TenantId, e.DriverId });
    }
}
