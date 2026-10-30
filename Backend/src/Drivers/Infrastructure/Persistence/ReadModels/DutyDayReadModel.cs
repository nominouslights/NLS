using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Drivers.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a <c>DutyDay</c> into <c>drivers.rm_duty_days</c>, maintained by
/// <c>DutyDayProjection</c>: the day's totals as the rule engine computed them (in minutes,
/// for the roster and the audit grid), the personal-conveyance figure, certification, and the
/// effective timeline as jsonb for a calendar to render without replaying the aggregate.
/// <see cref="LastStatus"/> is the <c>HosWire</c> string the roster's duty chip keys on. The
/// live figures a driver or dispatcher acts on come from the write side through the engine
/// (see <c>IDutyDayRepository</c>); this row is the copy, one projection poll behind.
/// </summary>
public sealed class DutyDayReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DriverId { get; set; }
    public DateOnly LocalDate { get; set; }
    public int DrivingMinutes { get; set; }
    public int OnDutyMinutes { get; set; }
    public int OffDutyMinutes { get; set; }
    public decimal PersonalConveyanceKm { get; set; }
    public string? LastStatus { get; set; }
    public bool Certified { get; set; }
    public DateTimeOffset? CertifiedAtUtc { get; set; }
    public bool RecertificationRequired { get; set; }
    public int ViolationCount { get; set; }
    public int EventCount { get; set; }

    /// <summary>The effective timeline, serialised as the API's <c>DutyEventResponse</c> array (jsonb).</summary>
    public string EventsJson { get; set; } = "[]";

    public int Version { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class DutyDayReadModelConfiguration : IEntityTypeConfiguration<DutyDayReadModel>
{
    public void Configure(EntityTypeBuilder<DutyDayReadModel> builder)
    {
        builder.HasKey(d => d.Id);
        builder.ToTable("rm_duty_days", DriversServiceCollectionExtensions.SchemaName);

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.TenantId).HasColumnName("tenant_id");
        builder.Property(d => d.DriverId).HasColumnName("driver_id");
        builder.Property(d => d.LocalDate).HasColumnName("local_date");
        builder.Property(d => d.DrivingMinutes).HasColumnName("driving_minutes");
        builder.Property(d => d.OnDutyMinutes).HasColumnName("on_duty_minutes");
        builder.Property(d => d.OffDutyMinutes).HasColumnName("off_duty_minutes");
        builder.Property(d => d.PersonalConveyanceKm).HasColumnName("pc_km").HasPrecision(9, 1);
        builder.Property(d => d.LastStatus).HasColumnName("last_status").HasMaxLength(32);
        builder.Property(d => d.Certified).HasColumnName("certified");
        builder.Property(d => d.CertifiedAtUtc).HasColumnName("certified_at_utc");
        builder.Property(d => d.RecertificationRequired).HasColumnName("recertification_required");
        builder.Property(d => d.ViolationCount).HasColumnName("violation_count");
        builder.Property(d => d.EventCount).HasColumnName("event_count");
        builder.Property(d => d.EventsJson).HasColumnName("events").HasColumnType("jsonb");
        builder.Property(d => d.Version).HasColumnName("version");
        builder.Property(d => d.UpdatedAtUtc).HasColumnName("updated_at_utc");

        // Mirrors the write table's one-record-per-day guarantee.
        builder.HasIndex(d => new { d.TenantId, d.DriverId, d.LocalDate }).IsUnique();
    }
}
