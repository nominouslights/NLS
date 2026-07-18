using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Identity.Domain.Users;

namespace NorthernLink.Identity.Infrastructure.Persistence.Configurations;

/// <summary>Maps the User aggregate to identity.users (snake_case columns).</summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(u => u.TenantId).HasColumnName("tenant_id");
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(256);
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(512);
        builder.Property(u => u.Role).HasColumnName("role").HasMaxLength(32);
        builder.Property(u => u.CreatedAtUtc).HasColumnName("created_at_utc");

        builder.HasIndex(u => u.Email).IsUnique();

        // DomainEvents ignore + Version concurrency token come from ModuleDbContext's
        // central aggregate conventions.
    }
}
