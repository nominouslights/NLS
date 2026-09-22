using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Billing.Domain.PurchaseOrders;

namespace NorthernLink.Billing.Infrastructure.Persistence;

/// <summary>
/// Maps the purchase-order replica to billing.purchase_order_snapshots (snake_case). The
/// primary key is the Clients module's PurchaseOrderId — the upsert key for the integration
/// consumer. The (tenant_id, client_id, po_number) index is the pricing lookup path.
/// </summary>
public sealed class PurchaseOrderSnapshotConfiguration : IEntityTypeConfiguration<PurchaseOrderSnapshot>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderSnapshot> builder)
    {
        builder.ToTable("purchase_order_snapshots");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(p => p.TenantId).HasColumnName("tenant_id");
        builder.Property(p => p.ClientId).HasColumnName("client_id");
        builder.Property(p => p.PoNumber).HasColumnName("po_number").HasMaxLength(64);
        builder.Property(p => p.Issued).HasColumnName("issued");
        builder.Property(p => p.Expiry).HasColumnName("expiry");
        builder.Property(p => p.AmountCad).HasColumnName("amount_cad").HasColumnType("numeric(12,2)");
        builder.Property(p => p.RoundTripRateCad)
            .HasColumnName("round_trip_rate_cad")
            .HasColumnType("numeric(12,2)");
        builder.Property(p => p.OneWayRateCad)
            .HasColumnName("one_way_rate_cad")
            .HasColumnType("numeric(12,2)");
        builder.Property(p => p.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(p => new { p.TenantId, p.ClientId, p.PoNumber });
    }
}
