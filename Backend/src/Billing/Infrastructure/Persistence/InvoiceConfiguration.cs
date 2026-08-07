using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Billing.Domain.Invoices;

namespace NorthernLink.Billing.Infrastructure.Persistence;

/// <summary>
/// Maps the Invoice aggregate to billing.invoices (snake_case columns). Lines are an
/// owned collection mapped to jsonb — one row per invoice, no child table (lines are
/// replaced wholesale while drafting, never addressed individually by SQL). Totals are
/// computed properties on the aggregate, not columns; the read model materializes them.
/// </summary>
public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(i => i.TenantId).HasColumnName("tenant_id");

        builder.Property(i => i.InvoiceNumber).HasColumnName("invoice_number").HasMaxLength(32);
        builder.Property(i => i.ClientId).HasColumnName("client_id");
        builder.Property(i => i.ClientName).HasColumnName("client_name").HasMaxLength(200);
        builder.Property(i => i.ContractId).HasColumnName("contract_id");
        builder.Property(i => i.PoNumber).HasColumnName("po_number").HasMaxLength(64);
        builder.Property(i => i.BudgetCode).HasColumnName("budget_code").HasMaxLength(64);
        builder.Property(i => i.NetTermsDays).HasColumnName("net_terms_days");
        builder.Property(i => i.GstApplicable).HasColumnName("gst_applicable");
        builder.Property(i => i.GstRate).HasColumnName("gst_rate").HasColumnType("numeric(5,4)");
        builder.Property(i => i.PeriodStart).HasColumnName("period_start");
        builder.Property(i => i.PeriodEnd).HasColumnName("period_end");

        builder.Property(i => i.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(i => i.IssuedAtUtc).HasColumnName("issued_at_utc");

        builder.Property(i => i.QboInvoiceId).HasColumnName("qbo_invoice_id").HasMaxLength(64);
        builder.Property(i => i.QboEnteredDate).HasColumnName("qbo_entered_date");
        builder.Property(i => i.PaymentConfirmedDate).HasColumnName("payment_confirmed_date");

        builder.Property(i => i.WrittenOffAmountCad).HasColumnName("written_off_amount_cad").HasPrecision(12, 2);
        builder.Property(i => i.WrittenOffDate).HasColumnName("written_off_date");
        builder.Property(i => i.WrittenOffReason).HasColumnName("written_off_reason").HasMaxLength(500);

        builder.OwnsMany(i => i.Lines, line =>
        {
            line.ToJson("lines");
            line.Property(l => l.Quantity).HasPrecision(6, 2);
            line.Property(l => l.UnitPriceCad).HasPrecision(12, 2);
            line.Property(l => l.AmountCad).HasPrecision(12, 2);
        });

        // The invoice-number sequence's authoritative guard (count-based generation).
        builder.HasIndex(i => new { i.TenantId, i.InvoiceNumber }).IsUnique();
        builder.HasIndex(i => new { i.TenantId, i.ClientId });
        builder.HasIndex(i => new { i.TenantId, i.Status });

        // DomainEvents ignore + Version concurrency token come from ModuleDbContext's
        // central aggregate conventions.
    }
}
