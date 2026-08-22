using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Trips.Domain.Riders;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// Maps the Rider aggregate to trips.riders (snake_case columns). The unique
/// (tenant_id, service_type, normalized_name) index is the directory's natural key and the
/// database backstop for the upsert pipeline's idempotency — a concurrent double-create of
/// the same rider fails there rather than forking the directory.
/// </summary>
public sealed class RiderConfiguration : IEntityTypeConfiguration<Rider>
{
    public void Configure(EntityTypeBuilder<Rider> builder)
    {
        builder.ToTable("riders");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(r => r.TenantId).HasColumnName("tenant_id");
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(r => r.NormalizedName).HasColumnName("normalized_name").HasMaxLength(200);

        builder.Property(r => r.ServiceType)
            .HasColumnName("service_type")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(r => r.Contact).HasColumnName("contact").HasMaxLength(200);
        builder.Property(r => r.RotationDays).HasColumnName("rotation_days");
        builder.Property(r => r.LastTripDate).HasColumnName("last_trip_date");
        builder.Property(r => r.LastTripNumber).HasColumnName("last_trip_number").HasMaxLength(32);
        builder.Property(r => r.TripCount).HasColumnName("trip_count");
        builder.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(r => r.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(r => new { r.TenantId, r.ServiceType, r.NormalizedName }).IsUnique();

        // DomainEvents ignore + Version concurrency token come from ModuleDbContext's
        // central aggregate conventions.
    }
}
