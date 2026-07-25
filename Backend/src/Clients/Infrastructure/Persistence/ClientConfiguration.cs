using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Clients.Domain.Clients;

namespace NorthernLink.Clients.Infrastructure.Persistence;

/// <summary>Maps the Client aggregate to clients.clients (snake_case columns).</summary>
public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(200);

        builder.Property(c => c.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(c => c.ServiceType)
            .HasColumnName("service_type")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(c => c.Tag).HasColumnName("tag").HasMaxLength(64);
        builder.Property(c => c.AgreementReference).HasColumnName("agreement_reference").HasMaxLength(128);
        builder.Property(c => c.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(c => new { c.TenantId, c.Name });
    }
}
