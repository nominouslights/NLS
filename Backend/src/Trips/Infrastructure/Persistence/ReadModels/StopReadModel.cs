using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a stop into <c>trips.rm_stops</c>. Address and coordinate are
/// fully flattened to plain columns. <see cref="Version"/> is the aggregate's concurrency
/// version at last projection.
/// </summary>
public sealed class StopReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = null!;
    public string? Type { get; set; }
    public string? Street { get; set; }
    public string City { get; set; } = null!;
    public string Province { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string Country { get; set; } = null!;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Notes { get; set; }
    public bool Active { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class StopReadModelConfiguration : IEntityTypeConfiguration<StopReadModel>
{
    public void Configure(EntityTypeBuilder<StopReadModel> builder)
    {
        builder.HasKey(s => s.Id);
        builder.ToTable("rm_stops", TripsServiceCollectionExtensions.SchemaName);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.TenantId).HasColumnName("tenant_id");
        builder.Property(s => s.Name).HasColumnName("name");
        builder.Property(s => s.Type).HasColumnName("type");
        builder.Property(s => s.Street).HasColumnName("street");
        builder.Property(s => s.City).HasColumnName("city");
        builder.Property(s => s.Province).HasColumnName("province");
        builder.Property(s => s.PostalCode).HasColumnName("postal_code");
        builder.Property(s => s.Country).HasColumnName("country");
        builder.Property(s => s.Latitude).HasColumnName("latitude");
        builder.Property(s => s.Longitude).HasColumnName("longitude");
        builder.Property(s => s.Notes).HasColumnName("notes");
        builder.Property(s => s.Active).HasColumnName("active");
        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(s => s.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(s => s.Version).HasColumnName("version");

        builder.HasIndex(s => new { s.TenantId, s.Active });
    }
}
