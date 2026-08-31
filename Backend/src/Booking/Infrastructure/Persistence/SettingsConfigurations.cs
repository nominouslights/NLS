using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Booking.Domain.Settings;

namespace NorthernLink.Booking.Infrastructure.Persistence;

/// <summary>
/// Maps the BookingPolicy aggregate to booking.booking_policies (snake_case columns).
/// The unique index on tenant_id enforces the one-policy-per-tenant singleton at the DB.
/// </summary>
public sealed class BookingPolicyConfiguration : IEntityTypeConfiguration<BookingPolicy>
{
    public void Configure(EntityTypeBuilder<BookingPolicy> builder)
    {
        builder.ToTable("booking_policies");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(p => p.TenantId).HasColumnName("tenant_id");
        builder.Property(p => p.CancellationWindowHours).HasColumnName("cancellation_window_hours");
        builder.Property(p => p.EarlyCancellationPenaltyCad)
            .HasColumnName("early_cancellation_penalty_cad")
            .HasColumnType("numeric(12,2)");
        builder.Property(p => p.BookingCutoffHours).HasColumnName("booking_cutoff_hours");
        builder.Property(p => p.SeatHoldMinutes).HasColumnName("seat_hold_minutes");
        builder.Property(p => p.DefaultPassengerMinimum).HasColumnName("default_passenger_minimum");
        builder.Property(p => p.DefaultSeatCapacity).HasColumnName("default_seat_capacity");
        builder.Property(p => p.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(p => p.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(p => p.TenantId).IsUnique();
    }
}

/// <summary>
/// Maps CorridorBookingSettings to booking.corridor_booking_settings (snake_case columns).
/// Unique (tenant_id, corridor_id) — one override row per corridor.
/// </summary>
public sealed class CorridorBookingSettingsConfiguration : IEntityTypeConfiguration<CorridorBookingSettings>
{
    public void Configure(EntityTypeBuilder<CorridorBookingSettings> builder)
    {
        builder.ToTable("corridor_booking_settings");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(s => s.TenantId).HasColumnName("tenant_id");
        builder.Property(s => s.CorridorId).HasColumnName("corridor_id");
        builder.Property(s => s.PassengerMinimum).HasColumnName("passenger_minimum");
        builder.Property(s => s.SeatCapacity).HasColumnName("seat_capacity");
        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(s => s.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(s => new { s.TenantId, s.CorridorId }).IsUnique();
    }
}
