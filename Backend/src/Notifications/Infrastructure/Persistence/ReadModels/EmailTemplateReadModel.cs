using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Notifications.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of an email template into <c>notifications.rm_email_templates</c> —
/// the row the Communications screen lists and the send dialog filters. Enum values are
/// projected as their PascalCase names.
/// </summary>
public sealed class EmailTemplateReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = null!;
    public string ServiceType { get; set; } = null!;
    public Guid? ClientId { get; set; }
    public string? ClientName { get; set; }
    public string Subject { get; set; } = null!;
    public string HtmlBody { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

/// <summary>Maps <see cref="EmailTemplateReadModel"/> to notifications.rm_email_templates.</summary>
public sealed class EmailTemplateReadModelConfiguration : IEntityTypeConfiguration<EmailTemplateReadModel>
{
    public void Configure(EntityTypeBuilder<EmailTemplateReadModel> builder)
    {
        builder.HasKey(t => t.Id);
        builder.ToTable("rm_email_templates", NotificationsServiceCollectionExtensions.SchemaName);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.TenantId).HasColumnName("tenant_id");
        builder.Property(t => t.Name).HasColumnName("name");
        builder.Property(t => t.ServiceType).HasColumnName("service_type");
        builder.Property(t => t.ClientId).HasColumnName("client_id");
        builder.Property(t => t.ClientName).HasColumnName("client_name");
        builder.Property(t => t.Subject).HasColumnName("subject");
        builder.Property(t => t.HtmlBody).HasColumnName("html_body");
        builder.Property(t => t.IsActive).HasColumnName("is_active");
        builder.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(t => t.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(t => t.Version).HasColumnName("version");
    }
}
