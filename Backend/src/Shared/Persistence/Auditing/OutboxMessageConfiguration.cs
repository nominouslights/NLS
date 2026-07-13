using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Shared.Persistence.Auditing;

/// <summary>
/// Maps <see cref="OutboxMessage"/> to <c>&lt;module-schema&gt;.outbox_messages</c>
/// (snake_case columns), with a partial index on undispatched rows so the dispatcher's
/// poll stays cheap as dispatched history accumulates.
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Position);
        builder.Property(m => m.Position).HasColumnName("position").UseIdentityAlwaysColumn();

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.TenantId).HasColumnName("tenant_id");
        builder.Property(m => m.EventType).HasColumnName("event_type").HasMaxLength(256);
        builder.Property(m => m.RoutingKey).HasColumnName("routing_key").HasMaxLength(128);
        builder.Property(m => m.Payload).HasColumnName("payload").HasColumnType("jsonb");
        builder.Property(m => m.OccurredAtUtc).HasColumnName("occurred_at_utc");
        builder.Property(m => m.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(m => m.DispatchedAtUtc).HasColumnName("dispatched_at_utc");
        builder.Property(m => m.Attempts).HasColumnName("attempts");
        builder.Property(m => m.LastError).HasColumnName("last_error");
        builder.Property(m => m.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc");

        builder.HasIndex(m => m.Id).IsUnique();
        builder.HasIndex(m => m.Position)
            .HasFilter("dispatched_at_utc IS NULL")
            .HasDatabaseName("ix_outbox_messages_pending");
    }
}
