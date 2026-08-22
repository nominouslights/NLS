using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Budgeting.Application.Integration;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>
/// Maps the user replica (upserted from <c>identity.user-changed</c> events) to
/// budgeting.user_lookup. Plain keyed rows — no audit pipeline, no concurrency token.
/// </summary>
public sealed class UserLookupConfiguration : IEntityTypeConfiguration<UserLookup>
{
    public void Configure(EntityTypeBuilder<UserLookup> builder)
    {
        builder.ToTable("user_lookup");

        builder.HasKey(u => u.UserId);
        builder.Property(u => u.UserId).HasColumnName("user_id").ValueGeneratedNever();
        builder.Property(u => u.TenantId).HasColumnName("tenant_id");

        // 256 matches identity.users.email — a replica column narrower than its source would
        // truncate silently at the upsert.
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(256);
        builder.Property(u => u.Role).HasColumnName("role").HasMaxLength(32);
        builder.Property(u => u.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(u => u.TenantId);
    }
}
