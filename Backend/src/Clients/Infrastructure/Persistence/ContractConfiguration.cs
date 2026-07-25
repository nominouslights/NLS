using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Clients.Domain.Contracts;

namespace NorthernLink.Clients.Infrastructure.Persistence;

/// <summary>Maps the Contract aggregate to clients.contracts (snake_case columns).</summary>
public sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("contracts");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.ClientId).HasColumnName("client_id");
        builder.Property(c => c.ClientName).HasColumnName("client_name").HasMaxLength(200);
        builder.Property(c => c.StartDate).HasColumnName("start_date");
        builder.Property(c => c.EndDate).HasColumnName("end_date");

        builder.Property(c => c.BillingModel)
            .HasColumnName("billing_model")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(c => c.RatePerRoundTripCad)
            .HasColumnName("rate_per_round_trip_cad")
            .HasColumnType("numeric(12,2)");

        builder.Property(c => c.GstApplicable).HasColumnName("gst_applicable");
        builder.Property(c => c.BudgetCode).HasColumnName("budget_code").HasMaxLength(64);

        builder.Property(c => c.BillingFrequency)
            .HasColumnName("billing_frequency")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(c => c.NetTermsDays).HasColumnName("net_terms_days");
        builder.Property(c => c.DefaultPoNumber).HasColumnName("default_po_number").HasMaxLength(64);

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(c => new { c.TenantId, c.ClientId });
    }
}
