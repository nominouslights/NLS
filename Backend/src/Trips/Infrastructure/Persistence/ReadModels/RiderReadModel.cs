using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a rider into <c>trips.rm_riders</c>. A straight mirror of the
/// aggregate; <see cref="Version"/> is the aggregate's concurrency version at last
/// projection. NextExpectedTravelDate is computed by the read service, not stored.
/// </summary>
public sealed class RiderReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = null!;
    public string NormalizedName { get; set; } = null!;
    public string ServiceType { get; set; } = null!;
    public string? Contact { get; set; }
    public int? RotationDays { get; set; }
    public DateOnly? LastTripDate { get; set; }
    public string? LastTripNumber { get; set; }
    public int TripCount { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class RiderReadModelConfiguration : IEntityTypeConfiguration<RiderReadModel>
{
    public void Configure(EntityTypeBuilder<RiderReadModel> builder)
    {
        builder.HasKey(r => r.Id);
        builder.ToTable("rm_riders", TripsServiceCollectionExtensions.SchemaName);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id");
        builder.Property(r => r.Name).HasColumnName("name");
        builder.Property(r => r.NormalizedName).HasColumnName("normalized_name");
        builder.Property(r => r.ServiceType).HasColumnName("service_type");
        builder.Property(r => r.Contact).HasColumnName("contact");
        builder.Property(r => r.RotationDays).HasColumnName("rotation_days");
        builder.Property(r => r.LastTripDate).HasColumnName("last_trip_date");
        builder.Property(r => r.LastTripNumber).HasColumnName("last_trip_number");
        builder.Property(r => r.TripCount).HasColumnName("trip_count");
        builder.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(r => r.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(r => r.Version).HasColumnName("version");

        builder.HasIndex(r => new { r.TenantId, r.ServiceType, r.Name });
    }
}
