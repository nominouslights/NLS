using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Billing.Domain.BillableTrips;

namespace NorthernLink.Billing.Infrastructure.Persistence;

/// <summary>
/// Maps the billable-trip replica to billing.billable_trips (snake_case). The primary key
/// is the Trips module's TripId — the insert-if-absent key for the integration consumer.
/// </summary>
public sealed class BillableTripConfiguration : IEntityTypeConfiguration<BillableTrip>
{
    public void Configure(EntityTypeBuilder<BillableTrip> builder)
    {
        builder.ToTable("billable_trips");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(t => t.TenantId).HasColumnName("tenant_id");
        builder.Property(t => t.TripNumber).HasColumnName("trip_number").HasMaxLength(32);
        builder.Property(t => t.ClientId).HasColumnName("client_id");
        builder.Property(t => t.ClientName).HasColumnName("client_name").HasMaxLength(200);
        builder.Property(t => t.ServiceType).HasColumnName("service_type").HasMaxLength(32);
        builder.Property(t => t.RouteName).HasColumnName("route_name").HasMaxLength(200);
        builder.Property(t => t.Origin).HasColumnName("origin").HasMaxLength(200);
        builder.Property(t => t.Destination).HasColumnName("destination").HasMaxLength(200);
        builder.Property(t => t.DistanceKm).HasColumnName("distance_km");
        builder.Property(t => t.ServiceDate).HasColumnName("service_date");
        builder.Property(t => t.RoundTripKey).HasColumnName("round_trip_key").HasMaxLength(64);
        builder.Property(t => t.Direction).HasColumnName("direction").HasMaxLength(16);
        builder.Property(t => t.PoNumber).HasColumnName("po_number").HasMaxLength(64);
        builder.Property(t => t.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(t => t.InvoiceId).HasColumnName("invoice_id");

        builder.Ignore(t => t.IsUninvoiced);

        // Draft generation's working set: client + period, uninvoiced.
        builder.HasIndex(t => new { t.TenantId, t.ClientId, t.ServiceDate });
        builder.HasIndex(t => new { t.TenantId, t.InvoiceId });
    }
}
