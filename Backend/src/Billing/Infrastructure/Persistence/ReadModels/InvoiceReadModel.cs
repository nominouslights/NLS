using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Billing.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a worksheet for the list view, maintained by
/// <c>InvoiceProjection</c> in <c>billing.rm_invoices</c>. Carries the computed totals plus
/// the manual QBO reference (<see cref="QboInvoiceId"/>, <see cref="QboEnteredDate"/>) and
/// the manually confirmed payment date (<see cref="PaymentConfirmedDate"/>) that drives the
/// outstanding/paid split. QuickBooks still owns sent/overdue and partial settlement — no
/// aging or balance fields here. Lines stay on the write row's
/// jsonb; the detail endpoint reads the aggregate. <see cref="Version"/> is the
/// aggregate's optimistic-concurrency version at last projection; staleness is
/// <c>event_journal.aggregate_version &gt; version</c>.
/// </summary>
public sealed class InvoiceReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = null!;
    public Guid? ContractId { get; set; }
    public string? PoNumber { get; set; }
    public string? BudgetCode { get; set; }
    public int NetTermsDays { get; set; }
    public bool GstApplicable { get; set; }
    public decimal GstRate { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public string Status { get; set; } = null!;
    public DateTimeOffset IssuedAtUtc { get; set; }
    public string? QboInvoiceId { get; set; }
    public DateOnly? QboEnteredDate { get; set; }

    /// <summary>Null while outstanding — the list view's paid/outstanding split is this field.</summary>
    public DateOnly? PaymentConfirmedDate { get; set; }

    public decimal? WrittenOffAmountCad { get; set; }
    public DateOnly? WrittenOffDate { get; set; }
    public string? WrittenOffReason { get; set; }

    /// <summary>Materialized so the receivables tiles are a sum, not a per-row status test.</summary>
    public decimal OutstandingCad { get; set; }

    // Derived columns, computed by the aggregate at projection time.
    public decimal SubtotalCad { get; set; }
    public decimal GstCad { get; set; }
    public decimal TotalCad { get; set; }
    public int LineCount { get; set; }

    public int Version { get; set; }
}

/// <summary>
/// Maps <see cref="InvoiceReadModel"/> to billing.rm_invoices. The tenant query filter is
/// applied centrally in <c>BillingDbContext.ConfigureModule</c> so it references the
/// context's per-instance TenantId, exactly like the aggregate filters.
/// </summary>
public sealed class InvoiceReadModelConfiguration : IEntityTypeConfiguration<InvoiceReadModel>
{
    public void Configure(EntityTypeBuilder<InvoiceReadModel> builder)
    {
        builder.ToTable("rm_invoices", BillingServiceCollectionExtensions.SchemaName);
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.TenantId).HasColumnName("tenant_id");
        builder.Property(i => i.InvoiceNumber).HasColumnName("invoice_number");
        builder.Property(i => i.ClientId).HasColumnName("client_id");
        builder.Property(i => i.ClientName).HasColumnName("client_name");
        builder.Property(i => i.ContractId).HasColumnName("contract_id");
        builder.Property(i => i.PoNumber).HasColumnName("po_number");
        builder.Property(i => i.BudgetCode).HasColumnName("budget_code");
        builder.Property(i => i.NetTermsDays).HasColumnName("net_terms_days");
        builder.Property(i => i.GstApplicable).HasColumnName("gst_applicable");
        builder.Property(i => i.GstRate).HasColumnName("gst_rate");
        builder.Property(i => i.PeriodStart).HasColumnName("period_start");
        builder.Property(i => i.PeriodEnd).HasColumnName("period_end");
        builder.Property(i => i.Status).HasColumnName("status");
        builder.Property(i => i.IssuedAtUtc).HasColumnName("issued_at_utc");
        builder.Property(i => i.QboInvoiceId).HasColumnName("qbo_invoice_id");
        builder.Property(i => i.QboEnteredDate).HasColumnName("qbo_entered_date");
        builder.Property(i => i.PaymentConfirmedDate).HasColumnName("payment_confirmed_date");
        builder.Property(i => i.WrittenOffAmountCad).HasColumnName("written_off_amount_cad").HasPrecision(12, 2);
        builder.Property(i => i.WrittenOffDate).HasColumnName("written_off_date");
        builder.Property(i => i.WrittenOffReason).HasColumnName("written_off_reason").HasMaxLength(500);
        builder.Property(i => i.OutstandingCad).HasColumnName("outstanding_cad").HasPrecision(12, 2);
        builder.Property(i => i.SubtotalCad).HasColumnName("subtotal_cad");
        builder.Property(i => i.GstCad).HasColumnName("gst_cad");
        builder.Property(i => i.TotalCad).HasColumnName("total_cad");
        builder.Property(i => i.LineCount).HasColumnName("line_count");
        builder.Property(i => i.Version).HasColumnName("version");

        builder.HasIndex(i => new { i.TenantId, i.Status });
        builder.HasIndex(i => new { i.TenantId, i.ClientId });
    }
}
