using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Billing.Domain.Contracts;

namespace NorthernLink.Billing.Infrastructure.Persistence;

/// <summary>
/// Maps the contract replica to billing.contract_snapshots (snake_case). The primary key
/// is the Clients module's ContractId — the upsert key for the integration consumer.
/// </summary>
public sealed class ContractSnapshotConfiguration : IEntityTypeConfiguration<ContractSnapshot>
{
    public void Configure(EntityTypeBuilder<ContractSnapshot> builder)
    {
        builder.ToTable("contract_snapshots");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.ClientId).HasColumnName("client_id");
        builder.Property(c => c.ClientName).HasColumnName("client_name").HasMaxLength(200);
        builder.Property(c => c.StartDate).HasColumnName("start_date");
        builder.Property(c => c.EndDate).HasColumnName("end_date");
        builder.Property(c => c.BillingModel).HasColumnName("billing_model").HasMaxLength(32);
        builder.Property(c => c.RatePerRoundTripCad)
            .HasColumnName("rate_per_round_trip_cad")
            .HasColumnType("numeric(12,2)");
        builder.Property(c => c.GstApplicable).HasColumnName("gst_applicable");
        builder.Property(c => c.BudgetCode).HasColumnName("budget_code").HasMaxLength(64);
        builder.Property(c => c.BillingFrequency).HasColumnName("billing_frequency").HasMaxLength(32);
        builder.Property(c => c.NetTermsDays).HasColumnName("net_terms_days");
        builder.Property(c => c.DefaultPoNumber).HasColumnName("default_po_number").HasMaxLength(64);
        builder.Property(c => c.Status).HasColumnName("status").HasMaxLength(32);
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(c => new { c.TenantId, c.ClientId });
    }
}
