using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Notifications.Application.Integration;

namespace NorthernLink.Notifications.Infrastructure.Persistence;

/// <summary>
/// Maps the user replica (upserted from <c>identity.user-changed</c> events) to
/// notifications.user_lookup. Plain keyed rows — no audit pipeline, no concurrency token —
/// column for column the same table as budgeting.user_lookup plus <c>is_active</c>.
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
        // truncate silently at the upsert; 128 matches identity.users.full_name likewise.
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(256);
        builder.Property(u => u.FullName).HasColumnName("full_name").HasMaxLength(128);
        builder.Property(u => u.Role).HasColumnName("role").HasMaxLength(32);
        builder.Property(u => u.IsActive).HasColumnName("is_active");
        builder.Property(u => u.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.Ignore(u => u.DisplayName);

        // The audience query is "tenant + role in (...) + active"; a tenant has a handful of
        // users, so the tenant index alone is plenty.
        builder.HasIndex(u => u.TenantId);
    }
}
