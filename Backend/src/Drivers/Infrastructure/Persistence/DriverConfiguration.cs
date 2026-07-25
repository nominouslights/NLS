using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Drivers.Domain.Drivers;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>Maps the Driver aggregate to drivers.drivers (snake_case columns).</summary>
public sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("drivers");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(d => d.TenantId).HasColumnName("tenant_id");
        builder.Property(d => d.Name).HasColumnName("name").HasMaxLength(128);
        builder.Property(d => d.Phone).HasColumnName("phone").HasMaxLength(32);
        builder.Property(d => d.LicenceClass).HasColumnName("licence_class").HasMaxLength(32);
        builder.Property(d => d.LicenceExpiry).HasColumnName("licence_expiry");
        builder.Property(d => d.Source).HasColumnName("source").HasMaxLength(128);
        builder.Property(d => d.HasWorkPermit).HasColumnName("has_work_permit");

        builder.Property(d => d.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(d => d.RegisteredAtUtc).HasColumnName("registered_at_utc");
        builder.Property(d => d.UpdatedAtUtc).HasColumnName("updated_at_utc");

        // Roster lists sort/search by name; duplicates are legal (two J. Spences can drive).
        builder.HasIndex(d => new { d.TenantId, d.Name });

        // DomainEvents ignore + Version concurrency token come from ModuleDbContext's
        // central aggregate conventions.
    }
}
