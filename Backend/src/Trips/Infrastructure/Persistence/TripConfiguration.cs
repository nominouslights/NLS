using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// Maps the Trip aggregate to trips.trips (snake_case columns). The route-stop snapshot
/// persists as owned jsonb; enums as strings. Two unique guards: the per-tenant trip
/// number, and (tenant, template, service date, direction) — the insert-if-absent
/// backstop that keeps concurrent generation-worker passes idempotent.
/// </summary>
public sealed class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("trips");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(t => t.TenantId).HasColumnName("tenant_id");
        builder.Property(t => t.TripNumber).HasColumnName("trip_number").HasMaxLength(32);
        builder.Property(t => t.ServiceDate).HasColumnName("service_date");
        builder.Property(t => t.WindowStart).HasColumnName("window_start");
        builder.Property(t => t.WindowEnd).HasColumnName("window_end");
        builder.Property(t => t.ServiceType)
            .HasColumnName("service_type")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(t => t.RouteId).HasColumnName("route_id");
        builder.Property(t => t.RouteName).HasColumnName("route_name").HasMaxLength(200);
        builder.Property(t => t.Origin).HasColumnName("origin").HasMaxLength(200);
        builder.Property(t => t.Destination).HasColumnName("destination").HasMaxLength(200);
        builder.OwnsMany(t => t.Stops, stop => stop.ToJson("stops"));
        builder.Property(t => t.DistanceKm).HasColumnName("distance_km");

        builder.Property(t => t.ScheduleTemplateId).HasColumnName("schedule_template_id");
        builder.Property(t => t.RoundTripKey).HasColumnName("round_trip_key").HasMaxLength(64);
        builder.Property(t => t.Direction)
            .HasColumnName("direction")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(t => t.IsEmptyLeg).HasColumnName("is_empty_leg");

        builder.Property(t => t.ClientId).HasColumnName("client_id");
        builder.Property(t => t.ClientName).HasColumnName("client_name").HasMaxLength(200);
        builder.Property(t => t.PoNumber).HasColumnName("po_number").HasMaxLength(64);

        builder.Property(t => t.DriverId).HasColumnName("driver_id");
        builder.Property(t => t.DriverName).HasColumnName("driver_name").HasMaxLength(128);
        builder.Property(t => t.VehicleId).HasColumnName("vehicle_id");
        builder.Property(t => t.VehicleUnit).HasColumnName("vehicle_unit").HasMaxLength(32);

        builder.Property(t => t.SeatsCapacity).HasColumnName("seats_capacity");
        builder.Property(t => t.SeatsConfirmed).HasColumnName("seats_confirmed");
        builder.Property(t => t.SeatsMinimum).HasColumnName("seats_minimum");
        builder.Property(t => t.DemandGuaranteed).HasColumnName("demand_guaranteed");

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(t => t.ManifestId).HasColumnName("manifest_id");
        builder.Property(t => t.HasPostTripInspection).HasColumnName("has_post_trip_inspection");
        builder.Property(t => t.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(t => t.CancelledReason).HasColumnName("cancelled_reason").HasMaxLength(500);

        builder.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(t => t.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.Ignore(t => t.IsTerminal);

        builder.HasIndex(t => new { t.TenantId, t.TripNumber }).IsUnique();
        builder.HasIndex(t => new { t.TenantId, t.ScheduleTemplateId, t.ServiceDate, t.Direction }).IsUnique();
        builder.HasIndex(t => new { t.TenantId, t.ServiceDate });
        builder.HasIndex(t => new { t.TenantId, t.DriverId });
        builder.HasIndex(t => new { t.TenantId, t.ClientId });
    }
}
