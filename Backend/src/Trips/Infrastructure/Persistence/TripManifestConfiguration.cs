using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// Maps the TripManifest aggregate to trips.trip_manifests (snake_case columns). The
/// row collections (§5 passengers, §6 cargo) are owned types mapped to jsonb — one row
/// per manifest, no child tables.
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

        // §5 — passengers as jsonb (pickup/dropoff are route-stop snapshot references).
        builder.OwnsMany(m => m.Passengers, passenger =>
        {
            passenger.ToJson("passengers");
            passenger.Property(p => p.FareAmountCad).HasPrecision(12, 2);

            // Explicit, because EF serializes an enum inside owned JSON as an integer by
            // default. A jsonb payload gets read by people and by hand-written SQL far more
            // often than a column does, and an opaque 0/1/2 in there is a trap.
            passenger.Property(p => p.FarePaymentMethod).HasConversion<string>();
        });
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

        // Provenance.
        builder.Property(m => m.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(m => m.EnteredBy).HasColumnName("entered_by").HasMaxLength(128);
        builder.Property(m => m.EnteredAt).HasColumnName("entered_at");

        builder.Property(m => m.CreatedAtUtc).HasColumnName("created_at_utc");

        builder.HasIndex(m => new { m.TenantId, m.TripNumber });

        // DomainEvents ignore + Version concurrency token come from ModuleDbContext's
        // central aggregate conventions.
    }
}
