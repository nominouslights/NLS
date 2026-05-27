using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class VehicleInspectionRecordConfiguration : IEntityTypeConfiguration<VehicleInspectionRecord>
{
    public void Configure(EntityTypeBuilder<VehicleInspectionRecord> builder)
    {
        builder.ToTable("vehicle_inspection_records");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.VehicleId).IsRequired();

        builder.Property(r => r.InspectionType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.InspectedAt).IsRequired();
        builder.Property(r => r.ExpiresAt);

        builder.Property(r => r.InspectorName).HasMaxLength(200);
        builder.Property(r => r.InspectionFacility).HasMaxLength(200);
        builder.Property(r => r.CertificateNumber).HasMaxLength(100);

        builder.Property(r => r.InspectionResult)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.DeficienciesNotes).HasMaxLength(2000);
        builder.Property(r => r.CorrectiveActionNotes).HasMaxLength(2000);
        builder.Property(r => r.CostDollars).HasPrecision(10, 2);
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.Ignore(r => r.IsExpiringSoon);

        builder.HasIndex(r => r.VehicleId);
        builder.HasIndex(r => new { r.VehicleId, r.InspectionType });
    }
}
