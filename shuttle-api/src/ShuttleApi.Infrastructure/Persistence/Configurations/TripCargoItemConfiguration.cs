using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class TripCargoItemConfiguration : IEntityTypeConfiguration<TripCargoItem>
{
    public void Configure(EntityTypeBuilder<TripCargoItem> builder)
    {
        builder.ToTable("trip_cargo_items");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.TripId).IsRequired();
        builder.Property(c => c.CargoType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(c => c.Description).HasMaxLength(200);
        builder.Property(c => c.Quantity).IsRequired();

        builder.HasIndex(c => c.TripId);
    }
}
