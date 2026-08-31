using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Booking.Domain.BookingDays;

namespace NorthernLink.Booking.Infrastructure.Persistence;

/// <summary>
/// Maps the BookingDay aggregate to booking.booking_days (snake_case columns). The unique
/// index on (tenant_id, corridor_id, service_date) IS the lazy-creation concurrency
/// guarantee — the repository's get-or-create relies on the DB rejecting a duplicate,
/// never on an application-level existence check.
/// </summary>
public sealed class BookingDayConfiguration : IEntityTypeConfiguration<BookingDay>
{
    public void Configure(EntityTypeBuilder<BookingDay> builder)
    {
        builder.ToTable("booking_days");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(d => d.TenantId).HasColumnName("tenant_id");
        builder.Property(d => d.CorridorId).HasColumnName("corridor_id");
        builder.Property(d => d.ServiceDate).HasColumnName("service_date");
        builder.Property(d => d.PassengerMinimumOverride).HasColumnName("passenger_minimum_override");
        builder.Property(d => d.SeatCapacityOverride).HasColumnName("seat_capacity_override");
        builder.Property(d => d.TripId).HasColumnName("trip_id");
        builder.Property(d => d.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(d => d.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(d => new { d.TenantId, d.CorridorId, d.ServiceDate }).IsUnique();
    }
}
