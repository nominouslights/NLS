using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("DomainEventLog");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.EventType).HasMaxLength(200).IsRequired();
        builder.Property(e => e.AggregateType).HasMaxLength(200).IsRequired();
        builder.Property(e => e.AggregateId).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.OccurredOn).IsRequired();
        builder.Property(e => e.CorrelationId).HasMaxLength(100);

        builder.HasIndex(e => e.AggregateId);
        builder.HasIndex(e => e.OccurredOn);
    }
}
