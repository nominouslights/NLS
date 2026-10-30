using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Drivers.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a <c>HosException</c> into <c>drivers.rm_hos_exceptions</c>,
/// maintained by <c>HosExceptionProjection</c>. <see cref="Kind"/> is the <c>HosWire</c> kind
/// string and <see cref="Status"/> the status name — the same strings the API emits — so the
/// compliance screen's grid reads this table directly. The live reads (status, days, audit) go
/// through the write side; this is the copy, one projection poll behind.
/// </summary>
public sealed class HosExceptionReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DriverId { get; set; }
    public string Kind { get; set; } = null!;
    public string Status { get; set; } = null!;
    public bool Applying { get; set; }
    public Guid? TripId { get; set; }
    public DateTimeOffset DeclaredAtUtc { get; set; }
    public Guid DeclaredByUserId { get; set; }
    public DateTimeOffset EffectiveFromUtc { get; set; }
    public DateTimeOffset? EffectiveToUtc { get; set; }
    public string Reason { get; set; } = null!;
    public string? Location { get; set; }
    public bool NotKnownBeforeDeparture { get; set; }
    public int? ExtraDrivingMinutes { get; set; }
    public int? DeferredOffDutyMinutes { get; set; }
    public bool EldRemarkRecorded { get; set; }
    public DateTimeOffset? DispatchAcknowledgedAtUtc { get; set; }
    public Guid? DispatchAcknowledgedByUserId { get; set; }
    public DateTimeOffset? ApprovedAtUtc { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public string? ClosureNote { get; set; }
    public int Version { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class HosExceptionReadModelConfiguration : IEntityTypeConfiguration<HosExceptionReadModel>
{
    public void Configure(EntityTypeBuilder<HosExceptionReadModel> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable("rm_hos_exceptions", DriversServiceCollectionExtensions.SchemaName);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.DriverId).HasColumnName("driver_id");
        builder.Property(e => e.Kind).HasColumnName("kind").HasMaxLength(64);
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(32);
        builder.Property(e => e.Applying).HasColumnName("applying");
        builder.Property(e => e.TripId).HasColumnName("trip_id");
        builder.Property(e => e.DeclaredAtUtc).HasColumnName("declared_at_utc");
        builder.Property(e => e.DeclaredByUserId).HasColumnName("declared_by_user_id");
        builder.Property(e => e.EffectiveFromUtc).HasColumnName("effective_from_utc");
        builder.Property(e => e.EffectiveToUtc).HasColumnName("effective_to_utc");
        builder.Property(e => e.Reason).HasColumnName("reason");
        builder.Property(e => e.Location).HasColumnName("location");
        builder.Property(e => e.NotKnownBeforeDeparture).HasColumnName("not_known_before_departure");
        builder.Property(e => e.ExtraDrivingMinutes).HasColumnName("extra_driving_minutes");
        builder.Property(e => e.DeferredOffDutyMinutes).HasColumnName("deferred_off_duty_minutes");
        builder.Property(e => e.EldRemarkRecorded).HasColumnName("eld_remark_recorded");
        builder.Property(e => e.DispatchAcknowledgedAtUtc).HasColumnName("dispatch_acknowledged_at_utc");
        builder.Property(e => e.DispatchAcknowledgedByUserId).HasColumnName("dispatch_acknowledged_by_user_id");
        builder.Property(e => e.ApprovedAtUtc).HasColumnName("approved_at_utc");
        builder.Property(e => e.ApprovedByUserId).HasColumnName("approved_by_user_id");
        builder.Property(e => e.ClosedAtUtc).HasColumnName("closed_at_utc");
        builder.Property(e => e.ClosedByUserId).HasColumnName("closed_by_user_id");
        builder.Property(e => e.ClosureNote).HasColumnName("closure_note");
        builder.Property(e => e.Version).HasColumnName("version");
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(e => new { e.TenantId, e.DriverId, e.EffectiveFromUtc });
    }
}
