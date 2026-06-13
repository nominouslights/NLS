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
        builder.Property(p => p.BoardingStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(PassengerBoardingStatus.NotBoarded);

        builder.Property(p => p.BookingReference).HasMaxLength(10);
        builder.Property(p => p.Phone).HasMaxLength(20);
        builder.Property(p => p.Email).HasMaxLength(200);
        builder.Property(p => p.Direction).HasMaxLength(20);
        builder.Property(p => p.CutoffDeadline);
        builder.Property(p => p.BookedAt).IsRequired();
        builder.Property(p => p.Fare).HasColumnType("numeric(10,2)");
        builder.Property(p => p.IsAddedAfterDeparture).IsRequired().HasDefaultValue(false);

        builder.HasIndex(p => p.TripId);
        builder.HasIndex(p => p.BookingReference)
            .IsUnique()
            .HasFilter("\"BookingReference\" IS NOT NULL");
    }
}
