using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class ClientEmailTemplateConfiguration
    : IEntityTypeConfiguration<ClientEmailTemplate>
{
    public void Configure(EntityTypeBuilder<ClientEmailTemplate> builder)
    {
        builder.ToTable("client_email_templates");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.ClientId).IsRequired();
        builder.Property(e => e.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Body).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();

        builder.Ignore(e => e.DomainEvents);

        builder.HasIndex(e => e.ClientId);
        builder.HasIndex(e => new { e.ClientId, e.Type }).IsUnique();
    }
}
