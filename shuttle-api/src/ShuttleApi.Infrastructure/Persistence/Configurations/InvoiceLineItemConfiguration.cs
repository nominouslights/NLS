using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Billing;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.ToTable("invoice_line_items");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.InvoiceId).IsRequired();
        builder.Property(l => l.TripId);
        builder.Property(l => l.LineType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(l => l.Description).HasMaxLength(500).IsRequired();
        builder.Property(l => l.UnitRate).HasPrecision(18, 2).IsRequired();
        builder.Property(l => l.Quantity).HasPrecision(18, 4).IsRequired();
        builder.Property(l => l.LineTotal).HasPrecision(18, 2).IsRequired();
        builder.Property(l => l.SortOrder).IsRequired();

        // FK relationship is owned by InvoiceConfiguration.HasMany(...).WithOne() — no second mapping here.
    }
}
