using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Drivers.Domain.Clearances;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>Maps the DriverClearance aggregate to drivers.driver_clearances (snake_case columns).</summary>
public sealed class DriverClearanceConfiguration : IEntityTypeConfiguration<DriverClearance>
{
    public void Configure(EntityTypeBuilder<DriverClearance> builder)
    {
        builder.ToTable("driver_clearances");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.DriverId).HasColumnName("driver_id");
        builder.Property(c => c.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(c => c.ClientName).HasColumnName("client_name").HasMaxLength(200);
        builder.Property(c => c.Expiry).HasColumnName("expiry");
        builder.Property(c => c.GrantedAtUtc).HasColumnName("granted_at_utc");

        builder.HasIndex(c => new { c.TenantId, c.DriverId });
    }
}
