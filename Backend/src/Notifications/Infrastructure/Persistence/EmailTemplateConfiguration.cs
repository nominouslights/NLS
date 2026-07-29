using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Notifications.Domain.Templates;

namespace NorthernLink.Notifications.Infrastructure.Persistence;

/// <summary>Maps the EmailTemplate aggregate to notifications.email_templates (snake_case columns).</summary>
public sealed class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.ToTable("email_templates");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(t => t.TenantId).HasColumnName("tenant_id");
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(EmailTemplate.NameMaxLength);

        builder.Property(t => t.ServiceType)
            .HasColumnName("service_type")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(t => t.ClientId).HasColumnName("client_id");
        builder.Property(t => t.ClientName).HasColumnName("client_name").HasMaxLength(200);
        builder.Property(t => t.Subject).HasColumnName("subject").HasMaxLength(EmailTemplate.SubjectMaxLength);
        builder.Property(t => t.HtmlBody).HasColumnName("html_body").HasMaxLength(EmailTemplate.HtmlBodyMaxLength);
        builder.Property(t => t.IsActive).HasColumnName("is_active");
        builder.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(t => t.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(t => new { t.TenantId, t.ServiceType });
        builder.HasIndex(t => new { t.TenantId, t.ClientId });

        // DomainEvents ignore + Version concurrency token come from ModuleDbContext's
        // central aggregate conventions.
    }
}
