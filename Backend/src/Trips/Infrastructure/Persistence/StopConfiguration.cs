using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Trips.Domain.Stops;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// Maps the Stop aggregate to trips.stops (snake_case columns). Address and Coordinate are
/// owned value objects flattened to plain columns (queryable, not jsonb) so the map layer
/// can filter/select on them later.
/// </summary>
public sealed class StopConfiguration : IEntityTypeConfiguration<Stop>
{
    public void Configure(EntityTypeBuilder<Stop> builder)
    {
        builder.ToTable("stops");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(s => s.TenantId).HasColumnName("tenant_id");
        builder.Property(s => s.Name).HasColumnName("name").HasMaxLength(200);

        builder.Property(s => s.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.OwnsOne(s => s.Address, address =>
        {
            address.Property(a => a.Street).HasColumnName("street").HasMaxLength(200);
            address.Property(a => a.City).HasColumnName("city").HasMaxLength(120).IsRequired();
            address.Property(a => a.Province).HasColumnName("province").HasMaxLength(120).IsRequired();
            address.Property(a => a.PostalCode).HasColumnName("postal_code").HasMaxLength(16);
            address.Property(a => a.Country).HasColumnName("country").HasMaxLength(120).IsRequired();
        });
        builder.Navigation(s => s.Address).IsRequired();

        builder.OwnsOne(s => s.Coordinate, coordinate =>
        {
            coordinate.Property(c => c.Latitude).HasColumnName("latitude");
            coordinate.Property(c => c.Longitude).HasColumnName("longitude");
        });
        builder.Navigation(s => s.Coordinate).IsRequired();

        builder.Property(s => s.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(s => s.Active).HasColumnName("active");
        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(s => s.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(s => new { s.TenantId, s.Name });
        builder.HasIndex(s => new { s.TenantId, s.Active });
    }
}
