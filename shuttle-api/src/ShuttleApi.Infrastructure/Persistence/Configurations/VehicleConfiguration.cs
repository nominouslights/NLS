using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");

        builder.Ignore(v => v.DomainEvents);

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.UnitCode).HasMaxLength(20).IsRequired();
        builder.HasIndex(v => v.UnitCode).IsUnique();

        builder.Property(v => v.VIN).HasMaxLength(17).IsRequired();
        builder.HasIndex(v => v.VIN).IsUnique();

        builder.Property(v => v.Make).HasMaxLength(100).IsRequired();
        builder.Property(v => v.Model).HasMaxLength(100).IsRequired();
        builder.Property(v => v.Year).IsRequired();
        builder.Property(v => v.Color).HasMaxLength(50).IsRequired();

        builder.Property(v => v.LicensePlate).HasMaxLength(15).IsRequired();
        builder.HasIndex(v => v.LicensePlate).IsUnique();

        builder.Property(v => v.Province).HasMaxLength(2).IsRequired();

        builder.Property(v => v.VehicleType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(v => v.PassengerCapacity).IsRequired();
        builder.Property(v => v.CurrentOdometerKm).IsRequired();
        builder.Property(v => v.AcquisitionDate).IsRequired();

        builder.Property(v => v.RegistrationExpiry);

        builder.Property(v => v.InsuranceProvider).HasMaxLength(200);
        builder.Property(v => v.InsurancePolicyNumber).HasMaxLength(100);
        builder.Property(v => v.InsuranceExpiry);

        builder.Property(v => v.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(v => v.StatusNote).HasMaxLength(500);

        builder.Property(v => v.IsActive).IsRequired();
        builder.Property(v => v.CreatedAt).IsRequired();
        builder.Property(v => v.Notes).HasMaxLength(2000);

        // Computed properties — not persisted
        builder.Ignore(v => v.IsRegistrationExpiringSoon);
        builder.Ignore(v => v.IsInsuranceExpiringSoon);

        builder.HasMany(v => v.ServiceRecords)
            .WithOne()
            .HasForeignKey(r => r.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.InspectionRecords)
            .WithOne()
            .HasForeignKey(r => r.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
