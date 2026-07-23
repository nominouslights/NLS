using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Trips.Domain.Routes;

namespace NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a route into <c>trips.rm_routes</c>. Origin/destination are
/// denormalized from the stop list; duration is flattened to whole minutes (the
/// response contract). <see cref="Version"/> is the aggregate's concurrency version at
/// last projection.
/// </summary>
public sealed class RouteReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = null!;
    public List<RouteStop> Stops { get; set; } = [];
    public string Origin { get; set; } = null!;
    public string Destination { get; set; } = null!;
    public int DistanceKm { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public string? RequiredLicenceClass { get; set; }
    public bool Active { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class RouteReadModelConfiguration : IEntityTypeConfiguration<RouteReadModel>
{
    public void Configure(EntityTypeBuilder<RouteReadModel> builder)
    {
        builder.HasKey(r => r.Id);
        builder.ToTable("rm_routes", TripsServiceCollectionExtensions.SchemaName);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id");
        builder.Property(r => r.Name).HasColumnName("name");
        builder.OwnsMany(r => r.Stops, stop => stop.ToJson("stops"));
        builder.Property(r => r.Origin).HasColumnName("origin");
        builder.Property(r => r.Destination).HasColumnName("destination");
        builder.Property(r => r.DistanceKm).HasColumnName("distance_km");
        builder.Property(r => r.EstimatedDurationMinutes).HasColumnName("estimated_duration_minutes");
        builder.Property(r => r.RequiredLicenceClass).HasColumnName("required_licence_class");
        builder.Property(r => r.Active).HasColumnName("active");
        builder.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(r => r.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(r => r.Version).HasColumnName("version");
    }
}
