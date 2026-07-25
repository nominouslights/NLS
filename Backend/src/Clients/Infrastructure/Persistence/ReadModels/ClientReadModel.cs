using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Clients.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a client, via <c>clients.rm_clients</c>. Carries a denormalized
/// active-contract summary (the contract in force today, or the next upcoming active one)
/// so the client profile screen reads one row — contract journal rows refresh these columns
/// through <c>ClientContractSummaryProjection</c>.
/// </summary>
public sealed class ClientReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string? ServiceType { get; set; }
    public string Tag { get; set; } = null!;
    public string? AgreementReference { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }

    // Active-contract summary — all null when the client has no active contract.
    public Guid? ActiveContractId { get; set; }
    public DateOnly? ActiveContractStartDate { get; set; }
    public DateOnly? ActiveContractEndDate { get; set; }
    public string? ActiveContractBillingModel { get; set; }
    public decimal? ActiveContractRatePerRoundTripCad { get; set; }
    public bool? ActiveContractGstApplicable { get; set; }
    public string? ActiveContractBudgetCode { get; set; }
    public string? ActiveContractBillingFrequency { get; set; }
    public int? ActiveContractNetTermsDays { get; set; }
    public string? ActiveContractDefaultPoNumber { get; set; }
}

public sealed class ClientReadModelConfiguration : IEntityTypeConfiguration<ClientReadModel>
{
    public void Configure(EntityTypeBuilder<ClientReadModel> builder)
    {
        builder.HasKey(c => c.Id);
        builder.ToTable("rm_clients", ClientsServiceCollectionExtensions.SchemaName);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.Name).HasColumnName("name");
        builder.Property(c => c.Type).HasColumnName("type");
        builder.Property(c => c.ServiceType).HasColumnName("service_type");
        builder.Property(c => c.Tag).HasColumnName("tag");
        builder.Property(c => c.AgreementReference).HasColumnName("agreement_reference");
        builder.Property(c => c.Notes).HasColumnName("notes");
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(c => c.Version).HasColumnName("version");

        builder.Property(c => c.ActiveContractId).HasColumnName("active_contract_id");
        builder.Property(c => c.ActiveContractStartDate).HasColumnName("active_contract_start_date");
        builder.Property(c => c.ActiveContractEndDate).HasColumnName("active_contract_end_date");
        builder.Property(c => c.ActiveContractBillingModel).HasColumnName("active_contract_billing_model");
        builder.Property(c => c.ActiveContractRatePerRoundTripCad)
            .HasColumnName("active_contract_rate_per_round_trip_cad")
            .HasColumnType("numeric(12,2)");
        builder.Property(c => c.ActiveContractGstApplicable).HasColumnName("active_contract_gst_applicable");
        builder.Property(c => c.ActiveContractBudgetCode).HasColumnName("active_contract_budget_code");
        builder.Property(c => c.ActiveContractBillingFrequency).HasColumnName("active_contract_billing_frequency");
        builder.Property(c => c.ActiveContractNetTermsDays).HasColumnName("active_contract_net_terms_days");
        builder.Property(c => c.ActiveContractDefaultPoNumber).HasColumnName("active_contract_default_po_number");
    }
}
