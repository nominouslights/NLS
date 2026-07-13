using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Fleet.Domain.Vehicles;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>
/// Maps retirement certificates to fleet.retirement_certificates. Tenant-scoped, so the
/// initial migration enables + forces RLS on this table exactly as for vehicles.
/// </summary>
public sealed class RetirementCertificateConfiguration : IEntityTypeConfiguration<RetirementCertificate>
{
    public void Configure(EntityTypeBuilder<RetirementCertificate> builder)
    {
        builder.ToTable("retirement_certificates");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.VehicleId).HasColumnName("vehicle_id");
        builder.Property(c => c.CertificateNumber).HasColumnName("certificate_number").HasMaxLength(32);
        builder.Property(c => c.Vin).HasColumnName("vin").HasColumnType("varchar(17)");
        builder.Property(c => c.UnitNumber).HasColumnName("unit_number").HasMaxLength(32);
        builder.Property(c => c.Make).HasColumnName("make").HasMaxLength(64);
        builder.Property(c => c.Model).HasColumnName("model").HasMaxLength(64);
        builder.Property(c => c.Year).HasColumnName("year");
        builder.Property(c => c.FinalOdometerKm).HasColumnName("final_odometer_km");
        builder.Property(c => c.RetirementReason).HasColumnName("retirement_reason").HasMaxLength(500);
        builder.Property(c => c.RetiredAtUtc).HasColumnName("retired_at_utc");
        builder.Property(c => c.IssuedAtUtc).HasColumnName("issued_at_utc");

        builder.HasIndex(c => new { c.TenantId, c.CertificateNumber }).IsUnique();
        builder.HasIndex(c => c.VehicleId);
    }
}
