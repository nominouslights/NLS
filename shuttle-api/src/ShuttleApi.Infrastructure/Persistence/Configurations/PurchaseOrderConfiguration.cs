using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_orders");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.ClientId).IsRequired();
        builder.Property(p => p.PoNumber).HasMaxLength(100).IsRequired();
        builder.Property(p => p.StartDate).IsRequired();
        builder.Property(p => p.Details).HasMaxLength(2000);
        builder.Property(p => p.TotalValue).HasPrecision(18, 2).IsRequired();

        builder.HasIndex(p => new { p.ClientId, p.PoNumber }).IsUnique();

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(p => p.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.LineItems)
            .WithOne()
            .HasForeignKey(i => i.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.LineItems).HasField("_lineItems");
    }
}
