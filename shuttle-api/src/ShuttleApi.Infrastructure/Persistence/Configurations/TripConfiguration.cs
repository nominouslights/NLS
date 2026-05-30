using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("trips");

        builder.Ignore(t => t.DomainEvents);

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.ClientId);
        builder.Property(t => t.VehicleId); // nullable — community trips start without a vehicle
        builder.Property(t => t.DriverId);
        builder.Property(t => t.ServiceType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(t => t.PurchaseOrderNumber).HasMaxLength(100);
        builder.Property(t => t.VehicleType).HasMaxLength(100);
        builder.Property(t => t.ScheduledAt).IsRequired();
        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(t => t.Notes).HasMaxLength(2000);
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.SeatCapacity);
        builder.Property(t => t.PricePerSeat).HasPrecision(10, 2);

        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.ClientId);
        builder.HasIndex(t => t.ServiceType);
        builder.HasIndex(t => new { t.DriverId, t.Status });

        builder.HasMany(t => t.Stops)
            .WithOne()
            .HasForeignKey(s => s.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Passengers)
            .WithOne()
            .HasForeignKey(p => p.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.PreInspection)
            .WithOne()
            .HasForeignKey<TripPreInspection>(p => p.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.PostReport)
            .WithOne()
            .HasForeignKey<TripPostReport>(r => r.TripId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
