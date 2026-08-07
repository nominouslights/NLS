using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a trip manifest (<c>trips.rm_trip_manifests</c>). The row
/// collections (§5 passengers, §6 cargo) are carried as jsonb verbatim and re-mapped with
/// the same <c>OwnsMany(...).ToJson(...)</c> shape as the aggregate, so this read model is
/// KEYED (on <see cref="Id"/>) — a keyless entity can't own a jsonb collection. Enum-typed
/// scalars are carried as their stored string forms. <see cref="Version"/> is the
/// aggregate's concurrency version at last refresh.
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

    // §5.
    public List<ManifestPassenger> Passengers { get; set; } = [];
    public bool AllSeatbeltsVerified { get; set; }

    // §6.
    public List<ManifestCargoItem> Cargo { get; set; } = [];
    public string? AllCargoSecured { get; set; }

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

        builder.OwnsMany(m => m.Passengers, passenger =>
        {
            passenger.ToJson("passengers");

            // Mirrors the aggregate's mapping exactly — same jsonb shape, same string-form
            // enum, or the projector would write names the read side can't parse back.
            passenger.Property(p => p.FareAmountCad).HasPrecision(12, 2);
            passenger.Property(p => p.FarePaymentMethod).HasConversion<string>();
        });
        builder.Property(m => m.AllSeatbeltsVerified).HasColumnName("all_seatbelts_verified");

        builder.OwnsMany(m => m.Cargo, cargo =>
        {
            cargo.ToJson("cargo");
            cargo.Property(c => c.WeightKg).HasPrecision(8, 2);
            cargo.Property(c => c.ChargeCad).HasPrecision(12, 2);
        });
        builder.Property(m => m.AllCargoSecured).HasColumnName("all_cargo_secured");

        builder.Property(m => m.Source).HasColumnName("source");
        builder.Property(m => m.EnteredBy).HasColumnName("entered_by");
        builder.Property(m => m.EnteredAt).HasColumnName("entered_at");
        builder.Property(m => m.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(m => m.Version).HasColumnName("version");
    }
}
