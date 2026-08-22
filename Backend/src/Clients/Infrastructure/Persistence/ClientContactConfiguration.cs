using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Clients.Domain.ClientContacts;

namespace NorthernLink.Clients.Infrastructure.Persistence;

/// <summary>Maps the ClientContact aggregate to clients.client_contacts (snake_case columns).</summary>
public sealed class ClientContactConfiguration : IEntityTypeConfiguration<ClientContact>
{
    public void Configure(EntityTypeBuilder<ClientContact> builder)
    {
        builder.ToTable("client_contacts");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.ClientId).HasColumnName("client_id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(c => c.Title).HasColumnName("title").HasMaxLength(128);
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(200);
        builder.Property(c => c.Phone).HasColumnName("phone").HasMaxLength(32);
        builder.Property(c => c.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(c => c.IsPrimary).HasColumnName("is_primary");
        builder.Property(c => c.ReceivesEmailReports).HasColumnName("receives_email_reports");
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(c => new { c.TenantId, c.ClientId });
    }
}
