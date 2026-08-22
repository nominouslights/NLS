using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a shipment into <c>trips.rm_shipments</c>. Enum-typed scalars are
/// carried as their string forms (the response contract is strings anyway).
/// <para>
/// <see cref="ClientId"/>/<see cref="ClientName"/> are the <b>shipment's own</b>. The carrying
/// trip's client is deliberately not denormalised here — it can change after the shipment is
/// projected, and the two are unrelated facts. Anywhere both are shown, the trip's is joined
/// from <c>rm_trips</c> at read time.
/// </para>
/// </summary>
public sealed class ShipmentReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string ShipmentNumber { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string Kind { get; set; } = null!;

    public Guid? ClientId { get; set; }
    public string? ClientName { get; set; }
    public string? PoNumber { get; set; }

    public string? ConsignorName { get; set; }
    public string? ConsignorContact { get; set; }
    public string? ConsigneeName { get; set; }
    public string? ConsigneeContact { get; set; }

    public Guid? OriginStopId { get; set; }
    public string OriginName { get; set; } = null!;
    public Guid? DestinationStopId { get; set; }
    public string DestinationName { get; set; } = null!;

    public string Description { get; set; } = null!;
    public int Pieces { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? LengthCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? HeightCm { get; set; }
    public bool Hazmat { get; set; }
    public decimal? DeclaredValueCad { get; set; }
    public string? SpecialHandling { get; set; }
    public bool Secured { get; set; }

    public decimal? ChargeCad { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTimeOffset? PaymentCollectedAtUtc { get; set; }

    public DateOnly? ReadyDate { get; set; }
    public DateOnly? RequiredByDate { get; set; }

    /// <summary>
    /// Denormalised routing rollup so the list can sort and filter without touching the leg
    /// table: the first leg's trip and the last leg's service date.
    /// </summary>
    public int LegCount { get; set; }

    public Guid? CurrentTripId { get; set; }
    public string? CurrentTripNumber { get; set; }
    public DateOnly? LastServiceDate { get; set; }

    /// <summary>
    /// Freight dropped at a hub with an onward leg still planned. Projected rather than derived
    /// at read time so the "stranded" filter is an indexable predicate.
    /// </summary>
    public bool AwaitingTransfer { get; set; }

    public DateTimeOffset? DeliveredAtUtc { get; set; }
    public string? ReceivedBy { get; set; }
    public string? DeliveryNote { get; set; }

    public string? CancelledReason { get; set; }
    public string? WrittenOffReason { get; set; }

    public string Source { get; set; } = null!;
    public string? EnteredBy { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class ShipmentReadModelConfiguration : IEntityTypeConfiguration<ShipmentReadModel>
{
    public void Configure(EntityTypeBuilder<ShipmentReadModel> builder)
    {
        builder.HasKey(s => s.Id);
        builder.ToTable("rm_shipments", TripsServiceCollectionExtensions.SchemaName);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.TenantId).HasColumnName("tenant_id");

        builder.Property(s => s.ShipmentNumber).HasColumnName("shipment_number");
        builder.Property(s => s.Status).HasColumnName("status");
        builder.Property(s => s.Kind).HasColumnName("kind");

        builder.Property(s => s.ClientId).HasColumnName("client_id");
        builder.Property(s => s.ClientName).HasColumnName("client_name");
        builder.Property(s => s.PoNumber).HasColumnName("po_number");

        builder.Property(s => s.ConsignorName).HasColumnName("consignor_name");
        builder.Property(s => s.ConsignorContact).HasColumnName("consignor_contact");
        builder.Property(s => s.ConsigneeName).HasColumnName("consignee_name");
        builder.Property(s => s.ConsigneeContact).HasColumnName("consignee_contact");

        builder.Property(s => s.OriginStopId).HasColumnName("origin_stop_id");
        builder.Property(s => s.OriginName).HasColumnName("origin_name");
        builder.Property(s => s.DestinationStopId).HasColumnName("destination_stop_id");
        builder.Property(s => s.DestinationName).HasColumnName("destination_name");

        builder.Property(s => s.Description).HasColumnName("description");
        builder.Property(s => s.Pieces).HasColumnName("pieces");
        builder.Property(s => s.WeightKg).HasColumnName("weight_kg").HasPrecision(8, 2);
        builder.Property(s => s.LengthCm).HasColumnName("length_cm").HasPrecision(8, 2);
        builder.Property(s => s.WidthCm).HasColumnName("width_cm").HasPrecision(8, 2);
        builder.Property(s => s.HeightCm).HasColumnName("height_cm").HasPrecision(8, 2);
        builder.Property(s => s.Hazmat).HasColumnName("hazmat");
        builder.Property(s => s.DeclaredValueCad).HasColumnName("declared_value_cad").HasPrecision(12, 2);
        builder.Property(s => s.SpecialHandling).HasColumnName("special_handling");
        builder.Property(s => s.Secured).HasColumnName("secured");

        builder.Property(s => s.ChargeCad).HasColumnName("charge_cad").HasPrecision(12, 2);
        builder.Property(s => s.PaymentMethod).HasColumnName("payment_method");
        builder.Property(s => s.PaymentCollectedAtUtc).HasColumnName("payment_collected_at_utc");

        builder.Property(s => s.ReadyDate).HasColumnName("ready_date");
        builder.Property(s => s.RequiredByDate).HasColumnName("required_by_date");

        builder.Property(s => s.LegCount).HasColumnName("leg_count");
        builder.Property(s => s.CurrentTripId).HasColumnName("current_trip_id");
        builder.Property(s => s.CurrentTripNumber).HasColumnName("current_trip_number");
        builder.Property(s => s.LastServiceDate).HasColumnName("last_service_date");
        builder.Property(s => s.AwaitingTransfer).HasColumnName("awaiting_transfer");

        builder.Property(s => s.DeliveredAtUtc).HasColumnName("delivered_at_utc");
        builder.Property(s => s.ReceivedBy).HasColumnName("received_by");
        builder.Property(s => s.DeliveryNote).HasColumnName("delivery_note");

        builder.Property(s => s.CancelledReason).HasColumnName("cancelled_reason");
        builder.Property(s => s.WrittenOffReason).HasColumnName("written_off_reason");

        builder.Property(s => s.Source).HasColumnName("source");
        builder.Property(s => s.EnteredBy).HasColumnName("entered_by");
        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(s => s.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(s => s.Version).HasColumnName("version");

        builder.HasIndex(s => new { s.TenantId, s.ShipmentNumber }).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.Status });

        // The cargo-invoice draft query: this client's uninvoiced shipments in a period.
        builder.HasIndex(s => new { s.TenantId, s.ClientId, s.Status });
    }
}
