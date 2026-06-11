using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class PurchaseOrderLineItemConfiguration : IEntityTypeConfiguration<PurchaseOrderLineItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLineItem> builder)
    {
        builder.ToTable("purchase_order_line_items");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.PurchaseOrderId).IsRequired();
        builder.Property(i => i.Description).HasMaxLength(200).IsRequired();
        builder.Property(i => i.UnitRate).HasPrecision(18, 2).IsRequired();
        builder.Property(i => i.Quantity).HasPrecision(18, 2).IsRequired();
        builder.Property(i => i.LineTotal).HasPrecision(18, 2).IsRequired();
        builder.Property(i => i.SortOrder).IsRequired();

        builder.HasIndex(i => i.PurchaseOrderId);
    }
}
