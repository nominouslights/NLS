using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Booking.Application.Integration;

namespace NorthernLink.Booking.Infrastructure.Persistence;

/// <summary>
/// Maps the corridor replica (upserted from <c>trips.route-changed</c> events) to
/// booking.corridor_lookup. Plain keyed rows — no audit pipeline, no concurrency token.
/// Mirrors Trips' VehicleLookup/DriverLookup configurations.
/// </summary>
public sealed class CorridorLookupConfiguration : IEntityTypeConfiguration<CorridorLookup>
{
    public void Configure(EntityTypeBuilder<CorridorLookup> builder)
    {
        builder.ToTable("corridor_lookup");

        builder.HasKey(c => c.CorridorId);
        builder.Property(c => c.CorridorId).HasColumnName("corridor_id").ValueGeneratedNever();
        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(c => c.Origin).HasColumnName("origin").HasMaxLength(200);
        builder.Property(c => c.Destination).HasColumnName("destination").HasMaxLength(200);
        builder.Property(c => c.Active).HasColumnName("active");
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(c => c.TenantId);
    }
}
