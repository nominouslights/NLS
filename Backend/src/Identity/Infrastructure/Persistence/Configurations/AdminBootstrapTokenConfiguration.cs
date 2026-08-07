using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Identity.Domain.Users;

namespace NorthernLink.Identity.Infrastructure.Persistence.Configurations;

/// <summary>Maps AdminBootstrapToken to identity.admin_bootstrap_tokens (snake_case columns).</summary>
public sealed class AdminBootstrapTokenConfiguration : IEntityTypeConfiguration<AdminBootstrapToken>
{
    public void Configure(EntityTypeBuilder<AdminBootstrapToken> builder)
    {
        builder.ToTable("admin_bootstrap_tokens");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(t => t.TenantId).HasColumnName("tenant_id");
        builder.Property(t => t.TokenHash).HasColumnName("token_hash").HasMaxLength(256);
        builder.Property(t => t.Role).HasColumnName("role").HasMaxLength(32);
        builder.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(t => t.ExpiresAtUtc).HasColumnName("expires_at_utc");
        builder.Property(t => t.ConsumedAtUtc).HasColumnName("consumed_at_utc");

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => new { t.TenantId, t.ConsumedAtUtc });
    }
}
