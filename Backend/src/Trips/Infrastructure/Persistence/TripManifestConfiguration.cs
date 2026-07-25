using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// Maps the TripManifest aggregate to trips.trip_manifests (snake_case columns). The
/// form's row collections (§3 pre-trip, §5 passengers, §6 cargo, §9 post-trip) are
/// owned types mapped to jsonb — one row per manifest, no child tables; the §4
/// multi-select sets and §7/§10 scalars are Postgres arrays.
/// </summary>
public sealed class TripManifestConfiguration : IEntityTypeConfiguration<TripManifest>
{
    public void Configure(EntityTypeBuilder<TripManifest> builder)
    {
        builder.ToTable("trip_manifests");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(m => m.TenantId).HasColumnName("tenant_id");

        // §1 — trip information.
        builder.Property(m => m.TripDate).HasColumnName("trip_date");
        builder.Property(m => m.TripNumber).HasColumnName("trip_number").HasMaxLength(32);
        builder.Property(m => m.Route).HasColumnName("route").HasMaxLength(200);
        builder.Property(m => m.Direction)
            .HasColumnName("direction")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(m => m.Client).HasColumnName("client").HasMaxLength(200);

        // §2 — vehicle & driver.
        builder.Property(m => m.Unit).HasColumnName("unit").HasMaxLength(32);
        builder.Property(m => m.DriverName).HasColumnName("driver_name").HasMaxLength(128);
        builder.Property(m => m.DriverLicenceNo).HasColumnName("driver_licence_no").HasMaxLength(64);
        builder.Property(m => m.LicencePlate).HasColumnName("licence_plate").HasMaxLength(32);
        builder.Property(m => m.OdometerStartKm).HasColumnName("odometer_start_km");
        builder.Property(m => m.FuelLevel)
            .HasColumnName("fuel_level")
            .HasConversion<string>()
            .HasMaxLength(16);

        // §3 — pre-trip inspection rows as jsonb.
        builder.OwnsMany(m => m.PreTripItems, item =>
        {
            item.ToJson("pre_trip_items");
            item.Property(i => i.Status).HasConversion<string>();
            item.Property(i => i.Severity).HasConversion<string>();
        });

        // §4 — weather & road conditions (multi-selects as text arrays).
        builder.PrimitiveCollection(m => m.Weather)
            .HasColumnName("weather")
            .ElementType(e => e.HasConversion<string>());
        builder.Property(m => m.TemperatureC).HasColumnName("temperature_c").HasMaxLength(16);
        builder.PrimitiveCollection(m => m.RoadConditions)
            .HasColumnName("road_conditions")
            .ElementType(e => e.HasConversion<string>());
        builder.Property(m => m.Visibility)
            .HasColumnName("visibility")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(m => m.RoadAdvisories).HasColumnName("road_advisories").HasMaxLength(500);

        // §5 — passengers as jsonb.
        builder.OwnsMany(m => m.Passengers, passenger => passenger.ToJson("passengers"));
        builder.Property(m => m.AllSeatbeltsVerified).HasColumnName("all_seatbelts_verified");

        // §6 — cargo as jsonb.
        builder.OwnsMany(m => m.Cargo, cargo =>
        {
            cargo.ToJson("cargo");
            cargo.Property(c => c.WeightKg).HasPrecision(8, 2);
            cargo.Property(c => c.ChargeCad).HasPrecision(12, 2);
        });
        builder.Property(m => m.AllCargoSecured)
            .HasColumnName("all_cargo_secured")
            .HasConversion<string>()
            .HasMaxLength(16);

        // §7 — issues log.
        builder.PrimitiveCollection(m => m.Issues).HasColumnName("issues");
        builder.Property(m => m.NoIssues).HasColumnName("no_issues");

        // §8 — trip completion log.
        builder.Property(m => m.DepartureTime).HasColumnName("departure_time").HasMaxLength(16);
        builder.Property(m => m.ArrivalTime).HasColumnName("arrival_time").HasMaxLength(16);
        builder.Property(m => m.OdometerEndKm).HasColumnName("odometer_end_km");
        builder.Property(m => m.TotalKm).HasColumnName("total_km");
        builder.Property(m => m.FuelAdded).HasColumnName("fuel_added");
        builder.Property(m => m.FuelLitres).HasColumnName("fuel_litres").HasColumnType("numeric(8,2)");
        builder.Property(m => m.FuelCostCad).HasColumnName("fuel_cost_cad").HasColumnType("numeric(12,2)");

        // §9 — post-trip check rows as jsonb.
        builder.OwnsMany(m => m.PostTripItems, item => item.ToJson("post_trip_items"));

        // §10 — certification.
        builder.PrimitiveCollection(m => m.Attestations).HasColumnName("attestations");
        builder.Property(m => m.DriverSignatureName).HasColumnName("driver_signature_name").HasMaxLength(128);
        builder.Property(m => m.CertifiedAt).HasColumnName("certified_at");

        // Provenance.
        builder.Property(m => m.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(m => m.EnteredBy).HasColumnName("entered_by").HasMaxLength(128);
        builder.Property(m => m.EnteredAt).HasColumnName("entered_at");

        builder.Property(m => m.CreatedAtUtc).HasColumnName("created_at_utc");

        builder.HasIndex(m => new { m.TenantId, m.TripNumber });
        builder.HasIndex(m => new { m.TenantId, m.Unit });

        // DomainEvents ignore + Version concurrency token come from ModuleDbContext's
        // central aggregate conventions.
    }
}
