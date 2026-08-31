using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Booking.Domain.Bookings;

namespace NorthernLink.Booking.Infrastructure.Persistence;

/// <summary>
/// Maps the Booking aggregate to booking.bookings (snake_case columns). The two
/// BookingLocation value objects are owned — flattened into pickup_*/dropoff_* columns.
/// Passengers are a real child table (see <see cref="BookingPassengerConfiguration"/>):
/// replacing the collection on update orphan-deletes the old rows (required FK + cascade).
/// </summary>
public sealed class BookingConfiguration : IEntityTypeConfiguration<Domain.Bookings.Booking>
{
    public void Configure(EntityTypeBuilder<Domain.Bookings.Booking> builder)
    {
        builder.ToTable("bookings");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(b => b.TenantId).HasColumnName("tenant_id");
        builder.Property(b => b.CustomerId).HasColumnName("customer_id");
        builder.Property(b => b.CustomerName).HasColumnName("customer_name").HasMaxLength(200);
        builder.Property(b => b.CorridorId).HasColumnName("corridor_id");
        builder.Property(b => b.CorridorName).HasColumnName("corridor_name").HasMaxLength(200);
        builder.Property(b => b.ServiceDate).HasColumnName("service_date");

        builder.Property(b => b.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.OwnsOne(b => b.Pickup, pickup =>
        {
            pickup.Property(l => l.StopId).HasColumnName("pickup_stop_id");
            pickup.Property(l => l.StopName).HasColumnName("pickup_stop_name").HasMaxLength(200);
            pickup.Property(l => l.AddressDetail).HasColumnName("pickup_address_detail").HasMaxLength(500);
        });

        builder.OwnsOne(b => b.Dropoff, dropoff =>
        {
            dropoff.Property(l => l.StopId).HasColumnName("dropoff_stop_id");
            dropoff.Property(l => l.StopName).HasColumnName("dropoff_stop_name").HasMaxLength(200);
            dropoff.Property(l => l.AddressDetail).HasColumnName("dropoff_address_detail").HasMaxLength(500);
        });

        builder.Navigation(b => b.Pickup).IsRequired();
        builder.Navigation(b => b.Dropoff).IsRequired();

        builder.Property(b => b.PaymentMethod)
            .HasColumnName("payment_method")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(b => b.PaymentStatus)
            .HasColumnName("payment_status")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(b => b.HoldExpiresAtUtc).HasColumnName("hold_expires_at_utc");
        builder.Property(b => b.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(b => b.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(b => b.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasMany(b => b.Passengers)
            .WithOne()
            .HasForeignKey(p => p.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(b => b.Passengers).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(b => new { b.TenantId, b.CorridorId, b.ServiceDate });
        builder.HasIndex(b => new { b.TenantId, b.CustomerId });
    }
}

/// <summary>Maps BookingPassenger child rows to booking.booking_passengers (snake_case).</summary>
public sealed class BookingPassengerConfiguration : IEntityTypeConfiguration<BookingPassenger>
{
    public void Configure(EntityTypeBuilder<BookingPassenger> builder)
    {
        builder.ToTable("booking_passengers");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(p => p.TenantId).HasColumnName("tenant_id");
        builder.Property(p => p.BookingId).HasColumnName("booking_id");
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(p => p.Phone).HasColumnName("phone").HasMaxLength(32);
        builder.Property(p => p.IsBillingCustomer).HasColumnName("is_billing_customer");

        builder.HasIndex(p => new { p.TenantId, p.BookingId });
    }
}
