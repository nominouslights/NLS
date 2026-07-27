using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Clients.Domain.ClientInteractions;

namespace NorthernLink.Clients.Infrastructure.Persistence.ReadModels;

/// <summary>Read-side projection of a client interaction, via <c>clients.rm_client_interactions</c>.</summary>
public sealed class ClientInteractionReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public InteractionType Type { get; set; }
    public DateOnly OccurredOn { get; set; }
    public string Summary { get; set; } = null!;
    public List<Guid> ParticipantContactIds { get; set; } = [];
    public DateOnly? FollowUpDate { get; set; }
    public string? FollowUpNote { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class ClientInteractionReadModelConfiguration : IEntityTypeConfiguration<ClientInteractionReadModel>
{
    public void Configure(EntityTypeBuilder<ClientInteractionReadModel> builder)
    {
        builder.HasKey(i => i.Id);
        builder.ToTable("rm_client_interactions", ClientsServiceCollectionExtensions.SchemaName);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.TenantId).HasColumnName("tenant_id");
        builder.Property(i => i.ClientId).HasColumnName("client_id");
        builder.Property(i => i.Type).HasColumnName("type").HasConversion<string>();
        builder.Property(i => i.OccurredOn).HasColumnName("occurred_on");
        builder.Property(i => i.Summary).HasColumnName("summary");
        builder.Property(i => i.ParticipantContactIds)
            .HasColumnName("participant_contact_ids")
            .HasColumnType("uuid[]");
        builder.Property(i => i.FollowUpDate).HasColumnName("follow_up_date");
        builder.Property(i => i.FollowUpNote).HasColumnName("follow_up_note");
        builder.Property(i => i.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(i => i.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(i => i.Version).HasColumnName("version");

        builder.HasIndex(i => new { i.TenantId, i.ClientId });
    }
}
