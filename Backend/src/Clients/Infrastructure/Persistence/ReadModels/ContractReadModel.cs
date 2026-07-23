using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Clients.Infrastructure.Persistence.ReadModels;

/// <summary>Read-side projection of a contract, via <c>clients.rm_contracts</c>.</summary>
public sealed class ContractReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string BillingModel { get; set; } = null!;
    public decimal? RatePerRoundTripCad { get; set; }
    public bool GstApplicable { get; set; }
    public string? BudgetCode { get; set; }
    public string BillingFrequency { get; set; } = null!;
    public int NetTermsDays { get; set; }
    public string? DefaultPoNumber { get; set; }
    public string Status { get; set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class ContractReadModelConfiguration : IEntityTypeConfiguration<ContractReadModel>
{
    public void Configure(EntityTypeBuilder<ContractReadModel> builder)
    {
        builder.HasKey(c => c.Id);
        builder.ToTable("rm_contracts", ClientsServiceCollectionExtensions.SchemaName);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.ClientId).HasColumnName("client_id");
        builder.Property(c => c.ClientName).HasColumnName("client_name");
        builder.Property(c => c.StartDate).HasColumnName("start_date");
        builder.Property(c => c.EndDate).HasColumnName("end_date");
        builder.Property(c => c.BillingModel).HasColumnName("billing_model");
        builder.Property(c => c.RatePerRoundTripCad)
            .HasColumnName("rate_per_round_trip_cad")
            .HasColumnType("numeric(12,2)");
        builder.Property(c => c.GstApplicable).HasColumnName("gst_applicable");
        builder.Property(c => c.BudgetCode).HasColumnName("budget_code");
        builder.Property(c => c.BillingFrequency).HasColumnName("billing_frequency");
        builder.Property(c => c.NetTermsDays).HasColumnName("net_terms_days");
        builder.Property(c => c.DefaultPoNumber).HasColumnName("default_po_number");
        builder.Property(c => c.Status).HasColumnName("status");
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(c => c.Version).HasColumnName("version");
    }
}
