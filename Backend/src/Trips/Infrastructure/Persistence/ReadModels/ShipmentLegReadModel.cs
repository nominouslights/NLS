using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of one shipment leg into <c>trips.rm_shipment_legs</c>, carrying a
/// denormalised copy of the shipment fields a trip's cargo section needs.
/// <para>
/// <b>Why denormalise here:</b> the trip manifest joins its trip by <c>TripNumber</c>, not
/// <c>TripId</c>, and its §6 has to render every parcel on that run — description, weight,
/// hazmat, and above all <see cref="ClientName"/>, since the parcel routinely bills to someone
/// other than the trip's client. Carrying those on the leg row makes that a single indexed
/// lookup with no join to <c>rm_shipments</c> and none to <c>trips.trips</c>.
/// </para>
/// <para>
/// The whole aggregate reprojects on any shipment event, so these copies cannot drift: a
/// description edit rewrites every one of that shipment's leg rows in the same pass.
/// </para>
/// </summary>
public sealed class ShipmentLegReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public Guid ShipmentId { get; set; }
    public int Sequence { get; set; }

    public Guid TripId { get; set; }
    public string TripNumber { get; set; } = null!;
    public DateOnly TripServiceDate { get; set; }

    public Guid? FromStopId { get; set; }
    public string FromName { get; set; } = null!;
    public Guid? ToStopId { get; set; }
    public string ToName { get; set; } = null!;

    public string Status { get; set; } = null!;
    public DateTimeOffset AssignedAtUtc { get; set; }
    public DateTimeOffset? PickedUpAtUtc { get; set; }
    public string? PickedUpBy { get; set; }
    public DateTimeOffset? DroppedAtUtc { get; set; }
    public string? DroppedBy { get; set; }

    /// <summary>True when no further leg follows — this run delivers rather than transfers.</summary>
    public bool IsFinalLeg { get; set; }

    /// <summary>The trip the goods move to next, when this leg is a hub transfer.</summary>
    public string? OnwardTripNumber { get; set; }

    // --- Denormalised shipment snapshot, rewritten on every reprojection of the aggregate. ---
    public string ShipmentNumber { get; set; } = null!;
    public string ShipmentStatus { get; set; } = null!;
    public string Kind { get; set; } = null!;
    public string Description { get; set; } = null!;

    /// <summary>The SHIPMENT's client — not the trip's. The whole point of showing it here.</summary>
    public Guid? ClientId { get; set; }
    public string? ClientName { get; set; }
    public string? ConsigneeName { get; set; }

    public int Pieces { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? ChargeCad { get; set; }
    public bool Hazmat { get; set; }
    public bool Secured { get; set; }

    public int Version { get; set; }
}

public sealed class ShipmentLegReadModelConfiguration : IEntityTypeConfiguration<ShipmentLegReadModel>
{
    public void Configure(EntityTypeBuilder<ShipmentLegReadModel> builder)
    {
        builder.HasKey(l => l.Id);
        builder.ToTable("rm_shipment_legs", TripsServiceCollectionExtensions.SchemaName);

        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.TenantId).HasColumnName("tenant_id");

        builder.Property(l => l.ShipmentId).HasColumnName("shipment_id");
        builder.Property(l => l.Sequence).HasColumnName("sequence");

        builder.Property(l => l.TripId).HasColumnName("trip_id");
        builder.Property(l => l.TripNumber).HasColumnName("trip_number");
        builder.Property(l => l.TripServiceDate).HasColumnName("trip_service_date");

        builder.Property(l => l.FromStopId).HasColumnName("from_stop_id");
        builder.Property(l => l.FromName).HasColumnName("from_name");
        builder.Property(l => l.ToStopId).HasColumnName("to_stop_id");
        builder.Property(l => l.ToName).HasColumnName("to_name");

        builder.Property(l => l.Status).HasColumnName("status");
        builder.Property(l => l.AssignedAtUtc).HasColumnName("assigned_at_utc");
        builder.Property(l => l.PickedUpAtUtc).HasColumnName("picked_up_at_utc");
        builder.Property(l => l.PickedUpBy).HasColumnName("picked_up_by");
        builder.Property(l => l.DroppedAtUtc).HasColumnName("dropped_at_utc");
        builder.Property(l => l.DroppedBy).HasColumnName("dropped_by");

        builder.Property(l => l.IsFinalLeg).HasColumnName("is_final_leg");
        builder.Property(l => l.OnwardTripNumber).HasColumnName("onward_trip_number");

        builder.Property(l => l.ShipmentNumber).HasColumnName("shipment_number");
        builder.Property(l => l.ShipmentStatus).HasColumnName("shipment_status");
        builder.Property(l => l.Kind).HasColumnName("kind");
        builder.Property(l => l.Description).HasColumnName("description");

        builder.Property(l => l.ClientId).HasColumnName("client_id");
        builder.Property(l => l.ClientName).HasColumnName("client_name");
        builder.Property(l => l.ConsigneeName).HasColumnName("consignee_name");

        builder.Property(l => l.Pieces).HasColumnName("pieces");
        builder.Property(l => l.WeightKg).HasColumnName("weight_kg").HasPrecision(8, 2);
        builder.Property(l => l.ChargeCad).HasColumnName("charge_cad").HasPrecision(12, 2);
        builder.Property(l => l.Hazmat).HasColumnName("hazmat");
        builder.Property(l => l.Secured).HasColumnName("secured");

        builder.Property(l => l.Version).HasColumnName("version");

        // The manifest's §6 lookup, and the trip detail's cargo section.
        builder.HasIndex(l => new { l.TenantId, l.TripNumber });
        builder.HasIndex(l => new { l.TenantId, l.TripId });
        builder.HasIndex(l => new { l.ShipmentId, l.Sequence }).IsUnique();
    }
}
