using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Trips.Domain.Routes;

namespace NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a trip into <c>trips.rm_trips</c> — every list/detail field
/// including the driver-name snapshot, attached manifest id, and demand. Enum-typed
/// scalars are carried as their string forms (the response contract is strings anyway);
/// the stop snapshot rides as jsonb re-using the domain's <see cref="RouteStop"/> shape.
/// <see cref="Version"/> is the aggregate's concurrency version at last projection.
/// </summary>
public sealed class TripReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string TripNumber { get; set; } = null!;
    public DateOnly ServiceDate { get; set; }
    public TimeOnly WindowStart { get; set; }
    public TimeOnly? WindowEnd { get; set; }
    public string ServiceType { get; set; } = null!;

    public Guid? RouteId { get; set; }
    public string RouteName { get; set; } = null!;
    public string Origin { get; set; } = null!;
    public string Destination { get; set; } = null!;
    public List<RouteStop> Stops { get; set; } = [];
    public int DistanceKm { get; set; }

    public Guid? ScheduleTemplateId { get; set; }
    public string? RoundTripKey { get; set; }
    public string? Direction { get; set; }
    public bool IsEmptyLeg { get; set; }

    public Guid? ClientId { get; set; }
    public string? ClientName { get; set; }
    public string? PoNumber { get; set; }

    public Guid? DriverId { get; set; }
    public string? DriverName { get; set; }
    public Guid? VehicleId { get; set; }
    public string? VehicleUnit { get; set; }

    public int? SeatsCapacity { get; set; }
    public int SeatsConfirmed { get; set; }
    public int? SeatsMinimum { get; set; }
    public bool DemandGuaranteed { get; set; }

    public string Status { get; set; } = null!;
    public Guid? ManifestId { get; set; }
    public bool HasPostTripInspection { get; set; }
    public DateTimeOffset? OperationsFinishedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? CancelledReason { get; set; }
    public string? WrittenOffReason { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class TripReadModelConfiguration : IEntityTypeConfiguration<TripReadModel>
{
    public void Configure(EntityTypeBuilder<TripReadModel> builder)
    {
        builder.HasKey(t => t.Id);
        builder.ToTable("rm_trips", TripsServiceCollectionExtensions.SchemaName);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.TenantId).HasColumnName("tenant_id");

        builder.Property(t => t.TripNumber).HasColumnName("trip_number");
        builder.Property(t => t.ServiceDate).HasColumnName("service_date");
        builder.Property(t => t.WindowStart).HasColumnName("window_start");
        builder.Property(t => t.WindowEnd).HasColumnName("window_end");
        builder.Property(t => t.ServiceType).HasColumnName("service_type");

        builder.Property(t => t.RouteId).HasColumnName("route_id");
        builder.Property(t => t.RouteName).HasColumnName("route_name");
        builder.Property(t => t.Origin).HasColumnName("origin");
        builder.Property(t => t.Destination).HasColumnName("destination");
        builder.OwnsMany(t => t.Stops, stop => stop.ToJson("stops"));
        builder.Property(t => t.DistanceKm).HasColumnName("distance_km");

        builder.Property(t => t.ScheduleTemplateId).HasColumnName("schedule_template_id");
        builder.Property(t => t.RoundTripKey).HasColumnName("round_trip_key");
        builder.Property(t => t.Direction).HasColumnName("direction");
        builder.Property(t => t.IsEmptyLeg).HasColumnName("is_empty_leg");

        builder.Property(t => t.ClientId).HasColumnName("client_id");
        builder.Property(t => t.ClientName).HasColumnName("client_name");
        builder.Property(t => t.PoNumber).HasColumnName("po_number");

        builder.Property(t => t.DriverId).HasColumnName("driver_id");
        builder.Property(t => t.DriverName).HasColumnName("driver_name");
        builder.Property(t => t.VehicleId).HasColumnName("vehicle_id");
        builder.Property(t => t.VehicleUnit).HasColumnName("vehicle_unit");

        builder.Property(t => t.SeatsCapacity).HasColumnName("seats_capacity");
        builder.Property(t => t.SeatsConfirmed).HasColumnName("seats_confirmed");
        builder.Property(t => t.SeatsMinimum).HasColumnName("seats_minimum");
        builder.Property(t => t.DemandGuaranteed).HasColumnName("demand_guaranteed");

        builder.Property(t => t.Status).HasColumnName("status");
        builder.Property(t => t.ManifestId).HasColumnName("manifest_id");
        builder.Property(t => t.HasPostTripInspection).HasColumnName("has_post_trip_inspection");
        builder.Property(t => t.OperationsFinishedAtUtc).HasColumnName("operations_finished_at_utc");
        builder.Property(t => t.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(t => t.CancelledReason).HasColumnName("cancelled_reason");
        builder.Property(t => t.WrittenOffReason).HasColumnName("written_off_reason");

        builder.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(t => t.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(t => t.Version).HasColumnName("version");

        // Narrow index for exact-date lookups (the dispatch board's "today").
        builder.HasIndex(t => new { t.TenantId, t.ServiceDate });

        // Covers the paged list's full sort key, so ORDER BY … OFFSET reads the index
        // instead of sorting the whole period on every page past the first.
        builder.HasIndex(t => new { t.TenantId, t.ServiceDate, t.WindowStart, t.TripNumber });
    }
}
