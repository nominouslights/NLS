using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class TripStopConfiguration : IEntityTypeConfiguration<TripStop>
{
    public void Configure(EntityTypeBuilder<TripStop> builder)
    {
        builder.ToTable("trip_stops");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.TripId).IsRequired();
        builder.Property(s => s.SequenceOrder).IsRequired();
        builder.Property(s => s.LocationName).HasMaxLength(300).IsRequired();
        builder.Property(s => s.Address).HasMaxLength(500);

        builder.HasIndex(s => new { s.TripId, s.SequenceOrder }).IsUnique();
    }
}
