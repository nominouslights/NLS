using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Shared.Persistence.Auditing;

/// <summary>
/// Maps <see cref="OutboxMessage"/> to <c>&lt;module-schema&gt;.outbox_messages</c>
/// (snake_case columns), with one partial index per delivery path — undispatched rows
/// for the RabbitMQ dispatcher, unprocessed rows for the polling consumer — so both
/// polls stay cheap as history accumulates.
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

        // Stored as text; the Pending default applies to pre-existing rows when the column
        // is added, which is what makes the polling consumer replay all history.
        builder.Property(m => m.ProcessingStatus)
            .HasColumnName("processing_status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(OutboxProcessingStatus.Pending);
        builder.Property(m => m.ProcessedAtUtc).HasColumnName("processed_at_utc");
        builder.Property(m => m.ProcessingAttempts).HasColumnName("processing_attempts").HasDefaultValue(0);
        builder.Property(m => m.ProcessingLastError).HasColumnName("processing_last_error");
        builder.Property(m => m.ProcessingNextAttemptAtUtc).HasColumnName("processing_next_attempt_at_utc");

        builder.HasIndex(m => m.Id).IsUnique();
        // Same column, two named indexes — the name in HasIndex keeps them distinct in the
        // EF model (an unnamed second HasIndex on the same column would replace the first).
        builder.HasIndex([nameof(OutboxMessage.Position)], "ix_outbox_messages_pending")
            .HasFilter("dispatched_at_utc IS NULL");
        builder.HasIndex([nameof(OutboxMessage.Position)], "ix_outbox_messages_unprocessed")
            .HasFilter("processing_status = 'Pending'");
    }
}
