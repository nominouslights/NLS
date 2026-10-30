using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Drivers.Infrastructure.Persistence.ReadModels;

/// <summary>Read-side projection of a <c>DriverOutOfService</c> into <c>drivers.rm_driver_out_of_service</c>, maintained by <c>DriverOutOfServiceProjection</c>.</summary>
public sealed class DriverOutOfServiceReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DriverId { get; set; }
    public string Status { get; set; } = null!;
    public DateTimeOffset DeclaredAtUtc { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public Guid RecordedByUserId { get; set; }
    public DateTimeOffset? UntilUtc { get; set; }

    /// <summary>The lift instant once lifted, else <see cref="UntilUtc"/> (null = open).</summary>
    public DateTimeOffset? EffectiveUntilUtc { get; set; }

    public string Reason { get; set; } = null!;
    public string? Evidence { get; set; }
    public DateTimeOffset? LiftedAtUtc { get; set; }
    public Guid? LiftedByUserId { get; set; }
    public string? LiftReason { get; set; }
    public int Version { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class DriverOutOfServiceReadModelConfiguration : IEntityTypeConfiguration<DriverOutOfServiceReadModel>
{
    public void Configure(EntityTypeBuilder<DriverOutOfServiceReadModel> builder)
    {
        builder.HasKey(r => r.Id);
        builder.ToTable("rm_driver_out_of_service", DriversServiceCollectionExtensions.SchemaName);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id");
        builder.Property(r => r.DriverId).HasColumnName("driver_id");
        builder.Property(r => r.Status).HasColumnName("status").HasMaxLength(32);
        builder.Property(r => r.DeclaredAtUtc).HasColumnName("declared_at_utc");
        builder.Property(r => r.RecordedAtUtc).HasColumnName("recorded_at_utc");
        builder.Property(r => r.RecordedByUserId).HasColumnName("recorded_by_user_id");
        builder.Property(r => r.UntilUtc).HasColumnName("until_utc");
        builder.Property(r => r.EffectiveUntilUtc).HasColumnName("effective_until_utc");
        builder.Property(r => r.Reason).HasColumnName("reason");
        builder.Property(r => r.Evidence).HasColumnName("evidence");
        builder.Property(r => r.LiftedAtUtc).HasColumnName("lifted_at_utc");
        builder.Property(r => r.LiftedByUserId).HasColumnName("lifted_by_user_id");
        builder.Property(r => r.LiftReason).HasColumnName("lift_reason");
        builder.Property(r => r.Version).HasColumnName("version");
        builder.Property(r => r.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(r => new { r.TenantId, r.DriverId, r.Status });
    }
}
