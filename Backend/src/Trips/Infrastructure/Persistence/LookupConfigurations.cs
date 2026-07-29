using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Trips.Application.Integration;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// Maps the driver replica (upserted from <c>drivers.driver-changed</c> events) to
/// trips.driver_lookup. Plain keyed rows — no audit pipeline, no concurrency token.
/// </summary>
public sealed class DriverLookupConfiguration : IEntityTypeConfiguration<DriverLookup>
{
    public void Configure(EntityTypeBuilder<DriverLookup> builder)
    {
        builder.ToTable("driver_lookup");

        builder.HasKey(d => d.DriverId);
        builder.Property(d => d.DriverId).HasColumnName("driver_id").ValueGeneratedNever();
        builder.Property(d => d.TenantId).HasColumnName("tenant_id");
        builder.Property(d => d.Name).HasColumnName("name").HasMaxLength(128);
        builder.Property(d => d.LicenceClass).HasColumnName("licence_class").HasMaxLength(16);
        builder.Property(d => d.Status).HasColumnName("status").HasMaxLength(16);
        builder.Property(d => d.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.Ignore(d => d.IsActive);

        builder.HasIndex(d => new { d.TenantId, d.Status });
    }
}

/// <summary>
/// Maps the vehicle replica (upserted from <c>fleet.vehicle-changed</c> events) to
/// trips.vehicle_lookup. Plain keyed rows — no audit pipeline, no concurrency token.
/// </summary>
public sealed class VehicleLookupConfiguration : IEntityTypeConfiguration<VehicleLookup>
{
    public void Configure(EntityTypeBuilder<VehicleLookup> builder)
    {
        builder.ToTable("vehicle_lookup");

        builder.HasKey(v => v.VehicleId);
        builder.Property(v => v.VehicleId).HasColumnName("vehicle_id").ValueGeneratedNever();
        builder.Property(v => v.TenantId).HasColumnName("tenant_id");
        builder.Property(v => v.UnitNumber).HasColumnName("unit_number").HasMaxLength(32);
        builder.Property(v => v.Status).HasColumnName("status").HasMaxLength(16);
        builder.Property(v => v.RequiredLicenceClass).HasColumnName("required_licence_class").HasMaxLength(16);
        builder.Property(v => v.SeatingCapacity).HasColumnName("seating_capacity");
        builder.Property(v => v.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.Ignore(v => v.IsActive);

        builder.HasIndex(v => new { v.TenantId, v.Status });
    }
}

/// <summary>
/// Maps the client replica (upserted from <c>clients.client-changed</c> events) to
/// trips.client_lookup. Plain keyed rows — no audit pipeline, no concurrency token.
/// </summary>
public sealed class ClientLookupConfiguration : IEntityTypeConfiguration<ClientLookup>
{
    public void Configure(EntityTypeBuilder<ClientLookup> builder)
    {
        builder.ToTable("client_lookup");

        builder.HasKey(c => c.ClientId);
        builder.Property(c => c.ClientId).HasColumnName("client_id").ValueGeneratedNever();
        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(c => c.Type).HasColumnName("type").HasMaxLength(32);
        builder.Property(c => c.ServiceType).HasColumnName("service_type").HasMaxLength(32);
        builder.Property(c => c.Tag).HasColumnName("tag").HasMaxLength(64);
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(c => c.TenantId);
    }
}
