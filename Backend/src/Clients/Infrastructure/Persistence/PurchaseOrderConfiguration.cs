using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Clients.Domain.PurchaseOrders;

namespace NorthernLink.Clients.Infrastructure.Persistence;

/// <summary>Maps the PurchaseOrder aggregate to clients.purchase_orders (snake_case columns).</summary>
public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_orders");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(p => p.TenantId).HasColumnName("tenant_id");
        builder.Property(p => p.ClientId).HasColumnName("client_id");
        builder.Property(p => p.PoNumber).HasColumnName("po_number").HasMaxLength(64);
        builder.Property(p => p.Issued).HasColumnName("issued");
        builder.Property(p => p.Expiry).HasColumnName("expiry");
        builder.Property(p => p.AmountCad).HasColumnName("amount_cad").HasColumnType("numeric(12,2)");
        builder.Property(p => p.Note).HasColumnName("note").HasMaxLength(1000);
        builder.Property(p => p.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(p => p.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(p => new { p.TenantId, p.ClientId });
    }
}
