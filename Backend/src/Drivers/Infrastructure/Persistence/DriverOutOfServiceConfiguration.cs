using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Drivers.Domain.Hos;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>Maps the DriverOutOfService aggregate to drivers.driver_out_of_service (snake_case columns; status as its name).</summary>
public sealed class DriverOutOfServiceConfiguration : IEntityTypeConfiguration<DriverOutOfService>
{
    public void Configure(EntityTypeBuilder<DriverOutOfService> builder)
    {
        builder.ToTable("driver_out_of_service");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(r => r.TenantId).HasColumnName("tenant_id");
        builder.Property(r => r.DriverId).HasColumnName("driver_id");
        builder.Property(r => r.DeclaredAtUtc).HasColumnName("declared_at_utc");
        builder.Property(r => r.RecordedAtUtc).HasColumnName("recorded_at_utc");
        builder.Property(r => r.RecordedByUserId).HasColumnName("recorded_by_user_id");
        builder.Property(r => r.UntilUtc).HasColumnName("until_utc");
        builder.Property(r => r.Reason).HasColumnName("reason").HasMaxLength(DriverOutOfService.ReasonMaxLength);
        builder.Property(r => r.Evidence).HasColumnName("evidence").HasMaxLength(DriverOutOfService.EvidenceMaxLength);
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32);
        builder.Property(r => r.LiftedAtUtc).HasColumnName("lifted_at_utc");
        builder.Property(r => r.LiftedByUserId).HasColumnName("lifted_by_user_id");
        builder.Property(r => r.LiftReason).HasColumnName("lift_reason").HasMaxLength(DriverOutOfService.ReasonMaxLength);
        builder.Property(r => r.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.Ignore(r => r.EffectiveUntilUtc);

        // "Is this driver out of service?" is asked on every gate check.
        builder.HasIndex(r => new { r.TenantId, r.DriverId, r.Status });
    }
}
