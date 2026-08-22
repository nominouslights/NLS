using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Clients.Infrastructure.Persistence.ReadModels;

/// <summary>Read-side projection of a client contact, via <c>clients.rm_client_contacts</c>.</summary>
public sealed class ClientContactReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public string Name { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public bool IsPrimary { get; set; }
    public bool ReceivesEmailReports { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class ClientContactReadModelConfiguration : IEntityTypeConfiguration<ClientContactReadModel>
{
    public void Configure(EntityTypeBuilder<ClientContactReadModel> builder)
    {
        builder.HasKey(c => c.Id);
        builder.ToTable("rm_client_contacts", ClientsServiceCollectionExtensions.SchemaName);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.ClientId).HasColumnName("client_id");
        builder.Property(c => c.Name).HasColumnName("name");
        builder.Property(c => c.Title).HasColumnName("title");
        builder.Property(c => c.Email).HasColumnName("email");
        builder.Property(c => c.Phone).HasColumnName("phone");
        builder.Property(c => c.Notes).HasColumnName("notes");
        builder.Property(c => c.IsPrimary).HasColumnName("is_primary");
        builder.Property(c => c.ReceivesEmailReports).HasColumnName("receives_email_reports");
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(c => c.Version).HasColumnName("version");

        builder.HasIndex(c => new { c.TenantId, c.ClientId });
    }
}
