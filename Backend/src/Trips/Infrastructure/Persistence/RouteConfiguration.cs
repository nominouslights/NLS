using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Trips.Domain.Routes;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// Maps the Route aggregate to trips.routes (snake_case columns). Stops persist as an
/// owned jsonb collection — one row per route, no child table.
/// </summary>
public sealed class RouteConfiguration : IEntityTypeConfiguration<Route>
{
    public void Configure(EntityTypeBuilder<Route> builder)
    {
        builder.ToTable("routes");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(r => r.TenantId).HasColumnName("tenant_id");
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(200);

        builder.OwnsMany(r => r.Stops, stop => stop.ToJson("stops"));

        builder.Property(r => r.DistanceKm).HasColumnName("distance_km");
        builder.Property(r => r.EstimatedDuration).HasColumnName("estimated_duration");
        builder.Property(r => r.RequiredLicenceClass).HasColumnName("required_licence_class").HasMaxLength(16);
        builder.Property(r => r.Active).HasColumnName("active");
        builder.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(r => r.UpdatedAtUtc).HasColumnName("updated_at_utc");

        // Origin/Destination are derived from Stops — never mapped.
        builder.Ignore(r => r.Origin);
        builder.Ignore(r => r.Destination);

        builder.HasIndex(r => new { r.TenantId, r.Name });
    }
}
