using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class VehicleFuelEntryConfiguration : IEntityTypeConfiguration<VehicleFuelEntry>
{
    public void Configure(EntityTypeBuilder<VehicleFuelEntry> builder)
    {
        builder.ToTable("vehicle_fuel_entries");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.VehicleId).IsRequired();
        builder.Property(e => e.FuelledAt).IsRequired();
        builder.Property(e => e.FuelLitres).HasPrecision(8, 2).IsRequired();
        builder.Property(e => e.TotalCostDollars).HasPrecision(10, 2).IsRequired();
        builder.Property(e => e.OdometerAtFuelling);
        builder.Property(e => e.ReceiptPhotoUrl).HasMaxLength(1000);
        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => e.VehicleId);
        builder.HasIndex(e => e.FuelledAt);
    }
}
