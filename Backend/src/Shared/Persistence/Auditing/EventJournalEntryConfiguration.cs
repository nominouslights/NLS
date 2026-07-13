using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Shared.Persistence.Auditing;

/// <summary>
/// Maps <see cref="EventJournalEntry"/> to <c>&lt;module-schema&gt;.event_journal</c>
/// (snake_case columns). Applied by <see cref="ModuleDbContext"/> for every module, so
/// the table lands in each module's own schema via the model's default schema.
/// </summary>
public sealed class EventJournalEntryConfiguration : IEntityTypeConfiguration<EventJournalEntry>
{
    public void Configure(EntityTypeBuilder<EventJournalEntry> builder)
    {
        builder.ToTable("event_journal");

        builder.HasKey(e => e.Position);
        builder.Property(e => e.Position).HasColumnName("position").UseIdentityAlwaysColumn();

        builder.Property(e => e.EventId).HasColumnName("event_id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.AggregateType).HasColumnName("aggregate_type").HasMaxLength(128);
        builder.Property(e => e.AggregateId).HasColumnName("aggregate_id");
        builder.Property(e => e.AggregateVersion).HasColumnName("aggregate_version");
        builder.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(256);
        builder.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb");
        builder.Property(e => e.OccurredAtUtc).HasColumnName("occurred_at_utc");
        builder.Property(e => e.RecordedAtUtc).HasColumnName("recorded_at_utc");
        builder.Property(e => e.ActorId).HasColumnName("actor_id");
        builder.Property(e => e.CorrelationId).HasColumnName("correlation_id");
        builder.Property(e => e.CausationId).HasColumnName("causation_id");

        builder.HasIndex(e => e.EventId).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.AggregateId, e.AggregateVersion });
    }
}
