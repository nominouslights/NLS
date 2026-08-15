using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Trips.Domain.Shipments;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// Maps the Shipment aggregate to trips.shipments (snake_case columns), with its route legs in
/// a real child table rather than jsonb.
/// <para>
/// <b>This is the codebase's first child-entity table — everything else owned is jsonb.</b> The
/// departure is deliberate: <c>Trip.Stops</c> and the manifest's passenger rows are jsonb because
/// nothing ever queries across them, whereas the two questions this feature exists to answer —
/// "what cargo is on TR-4824" and "everything for this client across whatever trips carried it"
/// — are both cross-row and both need an index. A jsonb array cannot serve either.
/// </para>
/// </summary>
public sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("shipments");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(s => s.TenantId).HasColumnName("tenant_id");
        builder.Property(s => s.ShipmentNumber).HasColumnName("shipment_number").HasMaxLength(32);

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(24);

        builder.Property(s => s.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(16);

        // The shipment's OWN client — routinely a different party from the client of any trip
        // that carries it. Nothing anywhere may default one from the other.
        builder.Property(s => s.ClientId).HasColumnName("client_id");
        builder.Property(s => s.ClientName).HasColumnName("client_name").HasMaxLength(200);
        builder.Property(s => s.PoNumber).HasColumnName("po_number").HasMaxLength(64);

        builder.Property(s => s.ConsignorName).HasColumnName("consignor_name").HasMaxLength(200);
        builder.Property(s => s.ConsignorContact).HasColumnName("consignor_contact").HasMaxLength(200);
        builder.Property(s => s.ConsigneeName).HasColumnName("consignee_name").HasMaxLength(200);
        builder.Property(s => s.ConsigneeContact).HasColumnName("consignee_contact").HasMaxLength(200);

        builder.Property(s => s.OriginStopId).HasColumnName("origin_stop_id");
        builder.Property(s => s.OriginName).HasColumnName("origin_name").HasMaxLength(200);
        builder.Property(s => s.DestinationStopId).HasColumnName("destination_stop_id");
        builder.Property(s => s.DestinationName).HasColumnName("destination_name").HasMaxLength(200);

        builder.Property(s => s.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(s => s.Pieces).HasColumnName("pieces");

        // Precision matches the old ManifestCargoItem.WeightKg mapping exactly, so the manifest
        // backfill in SeparateCargoFromManifest is a lossless cast.
        builder.Property(s => s.WeightKg).HasColumnName("weight_kg").HasPrecision(8, 2);
        builder.Property(s => s.LengthCm).HasColumnName("length_cm").HasPrecision(8, 2);
        builder.Property(s => s.WidthCm).HasColumnName("width_cm").HasPrecision(8, 2);
        builder.Property(s => s.HeightCm).HasColumnName("height_cm").HasPrecision(8, 2);
        builder.Property(s => s.Hazmat).HasColumnName("hazmat");
        builder.Property(s => s.DeclaredValueCad).HasColumnName("declared_value_cad").HasPrecision(12, 2);
        builder.Property(s => s.SpecialHandling).HasColumnName("special_handling").HasMaxLength(500);
        builder.Property(s => s.Secured).HasColumnName("secured");

        builder.Property(s => s.ChargeCad).HasColumnName("charge_cad").HasPrecision(12, 2);
        builder.Property(s => s.PaymentMethod)
            .HasColumnName("payment_method")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(s => s.PaymentCollectedAtUtc).HasColumnName("payment_collected_at_utc");

        builder.Property(s => s.ReadyDate).HasColumnName("ready_date");
        builder.Property(s => s.RequiredByDate).HasColumnName("required_by_date");

        builder.Property(s => s.DeliveredAtUtc).HasColumnName("delivered_at_utc");
        builder.Property(s => s.ReceivedBy).HasColumnName("received_by").HasMaxLength(200);
        builder.Property(s => s.DeliveryNote).HasColumnName("delivery_note").HasMaxLength(1000);

        builder.Property(s => s.CancelledReason).HasColumnName("cancelled_reason").HasMaxLength(500);
        builder.Property(s => s.WrittenOffReason).HasColumnName("written_off_reason").HasMaxLength(500);

        builder.Property(s => s.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(s => s.EnteredBy).HasColumnName("entered_by").HasMaxLength(200);
        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(s => s.UpdatedAtUtc).HasColumnName("updated_at_utc");

        // Read-only navigation over the aggregate's backing field — legs only ever change
        // through Shipment, which owns the ordering invariants.
        builder.HasMany(s => s.Legs)
            .WithOne()
            .HasForeignKey(l => l.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Shipment.Legs))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => new { s.TenantId, s.ShipmentNumber }).IsUnique();

        // The dispatcher's working queues: "what is unassigned", "what is ready to bill".
        builder.HasIndex(s => new { s.TenantId, s.Status });

        // "Everything for this client" — across whatever trips happened to carry it, which is
        // the query a cargo invoice is drafted from.
        builder.HasIndex(s => new { s.TenantId, s.ClientId });
    }
}

/// <summary>
/// Maps <see cref="ShipmentLeg"/> to trips.shipment_legs. Carries its own <c>tenant_id</c> and
/// its own RLS policy — isolation is never inherited through a foreign key.
/// </summary>
public sealed class ShipmentLegConfiguration : IEntityTypeConfiguration<ShipmentLeg>
{
    public void Configure(EntityTypeBuilder<ShipmentLeg> builder)
    {
        builder.ToTable("shipment_legs");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(l => l.TenantId).HasColumnName("tenant_id");
        builder.Property(l => l.ShipmentId).HasColumnName("shipment_id");
        builder.Property(l => l.Sequence).HasColumnName("sequence");

        builder.Property(l => l.TripId).HasColumnName("trip_id");
        builder.Property(l => l.TripNumber).HasColumnName("trip_number").HasMaxLength(32);
        builder.Property(l => l.TripServiceDate).HasColumnName("trip_service_date");

        builder.Property(l => l.FromStopId).HasColumnName("from_stop_id");
        builder.Property(l => l.FromName).HasColumnName("from_name").HasMaxLength(200);
        builder.Property(l => l.ToStopId).HasColumnName("to_stop_id");
        builder.Property(l => l.ToName).HasColumnName("to_name").HasMaxLength(200);

        builder.Property(l => l.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(l => l.AssignedAtUtc).HasColumnName("assigned_at_utc");
        builder.Property(l => l.PickedUpAtUtc).HasColumnName("picked_up_at_utc");
        builder.Property(l => l.PickedUpBy).HasColumnName("picked_up_by").HasMaxLength(200);
        builder.Property(l => l.DroppedAtUtc).HasColumnName("dropped_at_utc");
        builder.Property(l => l.DroppedBy).HasColumnName("dropped_by").HasMaxLength(200);

        // "What cargo is on this run" — the trip detail, the assign screen, and the manifest's
        // §6 all resolve through one of these two.
        builder.HasIndex(l => new { l.TenantId, l.TripId });
        builder.HasIndex(l => new { l.TenantId, l.TripNumber });
        builder.HasIndex(l => new { l.ShipmentId, l.Sequence }).IsUnique();
    }
}
