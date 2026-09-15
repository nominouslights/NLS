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

        // Nullable: every row predating profiles has no name, and null is what says so.
        // The length is User.ProfileFieldMaxLength so the column and the domain rule cannot
        // drift; Budgeting's user_lookup replica matches it for the same reason.
        builder.Property(u => u.FullName)
            .HasColumnName("full_name").HasMaxLength(User.ProfileFieldMaxLength);
        builder.Property(u => u.JobTitle)
            .HasColumnName("job_title").HasMaxLength(User.ProfileFieldMaxLength);

        builder.HasIndex(u => u.Email).IsUnique();

        // DomainEvents ignore + Version concurrency token come from ModuleDbContext's
        // central aggregate conventions.
    }
}
