using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class ContractPurchaseOrderConfiguration : IEntityTypeConfiguration<ContractPurchaseOrder>
{
    public void Configure(EntityTypeBuilder<ContractPurchaseOrder> builder)
    {
        builder.ToTable("contract_purchase_orders");

        builder.HasKey(x => new { x.ContractId, x.PurchaseOrderId });

        builder.HasOne<Contract>()
            .WithMany()
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<PurchaseOrder>()
            .WithMany()
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
