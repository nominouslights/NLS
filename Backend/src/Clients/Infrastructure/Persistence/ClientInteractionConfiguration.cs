using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Clients.Domain.ClientInteractions;

namespace NorthernLink.Clients.Infrastructure.Persistence;

/// <summary>Maps the ClientInteraction aggregate to clients.client_interactions (snake_case columns).</summary>
public sealed class ClientInteractionConfiguration : IEntityTypeConfiguration<ClientInteraction>
{
    public void Configure(EntityTypeBuilder<ClientInteraction> builder)
    {
        builder.ToTable("client_interactions");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(i => i.TenantId).HasColumnName("tenant_id");
        builder.Property(i => i.ClientId).HasColumnName("client_id");

        builder.Property(i => i.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(i => i.OccurredOn).HasColumnName("occurred_on");
        builder.Property(i => i.Summary).HasColumnName("summary").HasMaxLength(2000);
        builder.Property(i => i.ParticipantContactIds)
            .HasColumnName("participant_contact_ids")
            .HasColumnType("uuid[]");
        builder.Property(i => i.FollowUpDate).HasColumnName("follow_up_date");
        builder.Property(i => i.FollowUpNote).HasColumnName("follow_up_note").HasMaxLength(1000);
        builder.Property(i => i.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(i => i.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(i => new { i.TenantId, i.ClientId });
    }
}
