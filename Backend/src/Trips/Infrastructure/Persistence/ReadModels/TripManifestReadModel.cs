using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a trip manifest, via <c>trips.mv_trip_manifests</c> /
/// <c>trips.v_trip_manifests</c>. The form's row collections (§3/§5/§6/§9) are carried as
/// jsonb verbatim and re-mapped with the same <c>OwnsMany(...).ToJson(...)</c> shape as the
/// aggregate, so this read model is KEYED (on <see cref="Id"/>) — a keyless entity can't own
/// a jsonb collection. Enum-typed scalars and the §4 multi-selects are carried as their stored
/// string / text[] forms (the response contract is strings anyway). <see cref="Version"/> is
/// the aggregate's concurrency version at last refresh.
/// </summary>
public sealed class TripManifestReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    // §1.
    public DateOnly TripDate { get; set; }
    public string TripNumber { get; set; } = null!;
    public string Route { get; set; } = null!;
    public string? Direction { get; set; }
    public string? Client { get; set; }

    // §2.
    public string Unit { get; set; } = null!;
    public string DriverName { get; set; } = null!;
    public string? DriverLicenceNo { get; set; }
    public string? LicencePlate { get; set; }
    public int? OdometerStartKm { get; set; }
    public string? FuelLevel { get; set; }

    // §3.
    public List<PreTripChecklistItem> PreTripItems { get; set; } = [];

    // §4.
    public List<string> Weather { get; set; } = [];
    public string? TemperatureC { get; set; }
    public List<string> RoadConditions { get; set; } = [];
    public string? Visibility { get; set; }
    public string? RoadAdvisories { get; set; }

    // §5.
    public List<ManifestPassenger> Passengers { get; set; } = [];
    public bool AllSeatbeltsVerified { get; set; }

    // §6.
    public List<ManifestCargoItem> Cargo { get; set; } = [];
    public string? AllCargoSecured { get; set; }

    // §7.
    public List<string> Issues { get; set; } = [];
    public bool NoIssues { get; set; }

    // §8.
    public string? DepartureTime { get; set; }
    public string? ArrivalTime { get; set; }
    public int? OdometerEndKm { get; set; }
    public int? TotalKm { get; set; }
    public bool FuelAdded { get; set; }
    public decimal? FuelLitres { get; set; }
    public decimal? FuelCostCad { get; set; }

    // §9.
    public List<PostTripChecklistItem> PostTripItems { get; set; } = [];

    // §10.
    public List<bool> Attestations { get; set; } = [];
    public string DriverSignatureName { get; set; } = null!;
    public DateTimeOffset CertifiedAt { get; set; }

    // Provenance.
    public string Source { get; set; } = null!;
    public string? EnteredBy { get; set; }
    public DateTimeOffset? EnteredAt { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class TripManifestReadModelConfiguration : IEntityTypeConfiguration<TripManifestReadModel>
{
    public void Configure(EntityTypeBuilder<TripManifestReadModel> builder)
    {
        builder.HasKey(m => m.Id);
        builder.ToTable("rm_trip_manifests", TripsServiceCollectionExtensions.SchemaName);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.TenantId).HasColumnName("tenant_id");

        builder.Property(m => m.TripDate).HasColumnName("trip_date");
        builder.Property(m => m.TripNumber).HasColumnName("trip_number");
        builder.Property(m => m.Route).HasColumnName("route");
        builder.Property(m => m.Direction).HasColumnName("direction");
        builder.Property(m => m.Client).HasColumnName("client");

        builder.Property(m => m.Unit).HasColumnName("unit");
        builder.Property(m => m.DriverName).HasColumnName("driver_name");
        builder.Property(m => m.DriverLicenceNo).HasColumnName("driver_licence_no");
        builder.Property(m => m.LicencePlate).HasColumnName("licence_plate");
        builder.Property(m => m.OdometerStartKm).HasColumnName("odometer_start_km");
        builder.Property(m => m.FuelLevel).HasColumnName("fuel_level");

        builder.OwnsMany(m => m.PreTripItems, item =>
        {
            item.ToJson("pre_trip_items");
            item.Property(i => i.Status).HasConversion<string>();
            item.Property(i => i.Severity).HasConversion<string>();
        });

        builder.PrimitiveCollection(m => m.Weather).HasColumnName("weather");
        builder.Property(m => m.TemperatureC).HasColumnName("temperature_c");
        builder.PrimitiveCollection(m => m.RoadConditions).HasColumnName("road_conditions");
        builder.Property(m => m.Visibility).HasColumnName("visibility");
        builder.Property(m => m.RoadAdvisories).HasColumnName("road_advisories");

        builder.OwnsMany(m => m.Passengers, passenger => passenger.ToJson("passengers"));
        builder.Property(m => m.AllSeatbeltsVerified).HasColumnName("all_seatbelts_verified");

        builder.OwnsMany(m => m.Cargo, cargo =>
        {
            cargo.ToJson("cargo");
            cargo.Property(c => c.WeightKg).HasPrecision(8, 2);
            cargo.Property(c => c.ChargeCad).HasPrecision(12, 2);
        });
        builder.Property(m => m.AllCargoSecured).HasColumnName("all_cargo_secured");

        builder.PrimitiveCollection(m => m.Issues).HasColumnName("issues");
        builder.Property(m => m.NoIssues).HasColumnName("no_issues");

        builder.Property(m => m.DepartureTime).HasColumnName("departure_time");
        builder.Property(m => m.ArrivalTime).HasColumnName("arrival_time");
        builder.Property(m => m.OdometerEndKm).HasColumnName("odometer_end_km");
        builder.Property(m => m.TotalKm).HasColumnName("total_km");
        builder.Property(m => m.FuelAdded).HasColumnName("fuel_added");
        builder.Property(m => m.FuelLitres).HasColumnName("fuel_litres");
        builder.Property(m => m.FuelCostCad).HasColumnName("fuel_cost_cad");

        builder.OwnsMany(m => m.PostTripItems, item => item.ToJson("post_trip_items"));

        builder.PrimitiveCollection(m => m.Attestations).HasColumnName("attestations");
        builder.Property(m => m.DriverSignatureName).HasColumnName("driver_signature_name");
        builder.Property(m => m.CertifiedAt).HasColumnName("certified_at");

        builder.Property(m => m.Source).HasColumnName("source");
        builder.Property(m => m.EnteredBy).HasColumnName("entered_by");
        builder.Property(m => m.EnteredAt).HasColumnName("entered_at");
        builder.Property(m => m.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(m => m.Version).HasColumnName("version");
    }
}
