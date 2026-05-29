using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class TripPassengerConfiguration : IEntityTypeConfiguration<TripPassenger>
{
    public void Configure(EntityTypeBuilder<TripPassenger> builder)
    {
        builder.ToTable("trip_passengers");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.TripId).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.ContactInfo).HasMaxLength(200);
        builder.Property(p => p.SeatNumber);
        builder.Property(p => p.PaymentStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(p => p.TripId);
    }
}
