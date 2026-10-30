using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Clients.Infrastructure.Persistence.ReadModels;

/// <summary>Read-side projection of a purchase order, via <c>clients.rm_purchase_orders</c>.</summary>
public sealed class PurchaseOrderReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public string PoNumber { get; set; } = null!;
    public DateOnly Issued { get; set; }
    public DateOnly? Expiry { get; set; }
    public decimal? AmountCad { get; set; }

    /// <summary>This PO's own round-trip rate; null means the contract rate applies.</summary>
    public decimal? RoundTripRateCad { get; set; }

    /// <summary>This PO's own one-way rate; null means half the effective round-trip rate applies.</summary>
    public decimal? OneWayRateCad { get; set; }

    public string? Note { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class PurchaseOrderReadModelConfiguration : IEntityTypeConfiguration<PurchaseOrderReadModel>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderReadModel> builder)
    {
        builder.HasKey(p => p.Id);
        builder.ToTable("rm_purchase_orders", ClientsServiceCollectionExtensions.SchemaName);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.TenantId).HasColumnName("tenant_id");
        builder.Property(p => p.ClientId).HasColumnName("client_id");
        builder.Property(p => p.PoNumber).HasColumnName("po_number");
        builder.Property(p => p.Issued).HasColumnName("issued");
        builder.Property(p => p.Expiry).HasColumnName("expiry");
        builder.Property(p => p.AmountCad).HasColumnName("amount_cad").HasColumnType("numeric(12,2)");
        builder.Property(p => p.RoundTripRateCad)
            .HasColumnName("round_trip_rate_cad")
            .HasColumnType("numeric(12,2)");
        builder.Property(p => p.OneWayRateCad)
            .HasColumnName("one_way_rate_cad")
            .HasColumnType("numeric(12,2)");
        builder.Property(p => p.Note).HasColumnName("note");
        builder.Property(p => p.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(p => p.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(p => p.Version).HasColumnName("version");
    }
}
