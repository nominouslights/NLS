using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class ClientNotificationEmailConfiguration
    : IEntityTypeConfiguration<ClientNotificationEmail>
{
    public void Configure(EntityTypeBuilder<ClientNotificationEmail> builder)
    {
        builder.ToTable("client_notification_emails");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.ClientId).IsRequired();
        builder.Property(e => e.Category)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(e => e.Email).HasMaxLength(320).IsRequired();

        builder.HasIndex(e => e.ClientId);
        builder.HasIndex(e => new { e.ClientId, e.Category, e.Email }).IsUnique();
    }
}
