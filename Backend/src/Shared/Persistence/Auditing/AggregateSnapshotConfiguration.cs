using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Shared.Persistence.Auditing;

/// <summary>
/// Maps <see cref="AggregateSnapshot"/> to <c>&lt;module-schema&gt;.aggregate_snapshots</c>
/// (snake_case columns). The composite primary key (aggregate_id, version) is both the
/// required uniqueness and the viewer's core access path: all versions of one aggregate,
/// ordered, is a primary-key range scan.
/// </summary>
public sealed class AggregateSnapshotConfiguration : IEntityTypeConfiguration<AggregateSnapshot>
{
    public void Configure(EntityTypeBuilder<AggregateSnapshot> builder)
    {
        builder.ToTable("aggregate_snapshots");

        builder.HasKey(s => new { s.AggregateId, s.Version });
        builder.Property(s => s.AggregateId).HasColumnName("aggregate_id");
        builder.Property(s => s.Version).HasColumnName("version");

        builder.Property(s => s.TenantId).HasColumnName("tenant_id");
        builder.Property(s => s.AggregateType).HasColumnName("aggregate_type").HasMaxLength(128);
        builder.Property(s => s.State).HasColumnName("state").HasColumnType("jsonb");
        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(s => s.ActorId).HasColumnName("actor_id");
        builder.Property(s => s.CorrelationId).HasColumnName("correlation_id");

        builder.HasIndex(s => new { s.TenantId, s.AggregateType, s.CreatedAtUtc });
    }
}
