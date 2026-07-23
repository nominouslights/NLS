using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Drivers.Domain.Credentials;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>Maps the DriverCredential aggregate to drivers.driver_credentials (snake_case columns).</summary>
public sealed class DriverCredentialConfiguration : IEntityTypeConfiguration<DriverCredential>
{
    public void Configure(EntityTypeBuilder<DriverCredential> builder)
    {
        builder.ToTable("driver_credentials");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.DriverId).HasColumnName("driver_id");
        builder.Property(c => c.Type).HasColumnName("type").HasMaxLength(64);
        builder.Property(c => c.Label).HasColumnName("label").HasMaxLength(200);
        builder.Property(c => c.Issued).HasColumnName("issued");
        builder.Property(c => c.Expiry).HasColumnName("expiry");
        builder.Property(c => c.Optional).HasColumnName("optional");
        builder.Property(c => c.Note).HasColumnName("note").HasMaxLength(1000);
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(c => c.ImageKey).HasColumnName("image_key").HasMaxLength(500);
        builder.Property(c => c.ImageContentType).HasColumnName("image_content_type").HasMaxLength(100);

        builder.HasIndex(c => new { c.TenantId, c.DriverId });
    }
}
