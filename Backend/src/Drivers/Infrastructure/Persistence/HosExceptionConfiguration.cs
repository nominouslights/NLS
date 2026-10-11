using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Drivers.Domain.Hos;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>Maps the HosException aggregate to drivers.hos_exceptions (snake_case columns; enums as their names).</summary>
public sealed class HosExceptionConfiguration : IEntityTypeConfiguration<HosException>
{
    public void Configure(EntityTypeBuilder<HosException> builder)
    {
        builder.ToTable("hos_exceptions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.DriverId).HasColumnName("driver_id");
        builder.Property(e => e.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.TripId).HasColumnName("trip_id");
        builder.Property(e => e.DeclaredAtUtc).HasColumnName("declared_at_utc");
        builder.Property(e => e.DeclaredByUserId).HasColumnName("declared_by_user_id");
        builder.Property(e => e.EffectiveFromUtc).HasColumnName("effective_from_utc");
        builder.Property(e => e.EffectiveToUtc).HasColumnName("effective_to_utc");
        builder.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(HosException.ReasonMaxLength);
        builder.Property(e => e.Location).HasColumnName("location").HasMaxLength(HosException.LocationMaxLength);
        builder.Property(e => e.NotKnownBeforeDeparture).HasColumnName("not_known_before_departure");
        builder.Property(e => e.ExtraDriving).HasColumnName("extra_driving");
        builder.Property(e => e.DeferredOffDuty).HasColumnName("deferred_off_duty");
        builder.Property(e => e.EldRemarkRecorded).HasColumnName("eld_remark_recorded");
        builder.Property(e => e.DispatchAcknowledgedAtUtc).HasColumnName("dispatch_acknowledged_at_utc");
        builder.Property(e => e.DispatchAcknowledgedByUserId).HasColumnName("dispatch_acknowledged_by_user_id");
        builder.Property(e => e.ApprovedAtUtc).HasColumnName("approved_at_utc");
        builder.Property(e => e.ApprovedByUserId).HasColumnName("approved_by_user_id");
        builder.Property(e => e.ClosedAtUtc).HasColumnName("closed_at_utc");
        builder.Property(e => e.ClosedByUserId).HasColumnName("closed_by_user_id");
        builder.Property(e => e.ClosureNote).HasColumnName("closure_note").HasMaxLength(HosException.NoteMaxLength);
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.Ignore(e => e.IsApplying);

        // The ledger loads a driver's exceptions touching a window.
        builder.HasIndex(e => new { e.TenantId, e.DriverId, e.EffectiveFromUtc });

        // DomainEvents ignore + Version concurrency token come from ModuleDbContext's
        // central aggregate conventions.
    }
}
