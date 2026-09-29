using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of an allocation line into <c>budgeting.rm_budget_allocations</c> — the
/// rows the period dashboard lists and the period totals are summed from. A mutable class rather
/// than a record, like every read model on the platform (see <see cref="BudgetCodeReadModel"/>
/// for why); the wire contract is <see cref="Application.Allocations.BudgetAllocationResponse"/>.
/// <para>
/// <b>No code name, category or service line here.</b> Those describe the code, not the line,
/// and the projection base only ever reads its own source aggregate — a category copied onto
/// this row would go stale the moment a planner re-classified the code, and the period's totals
/// would quietly disagree with its lines. The read services resolve them from
/// <c>rm_budget_codes</c> on every read instead.
/// </para>
/// </summary>
public sealed class BudgetAllocationReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PeriodId { get; set; }
    public Guid BudgetCodeId { get; set; }
    public string Code { get; set; } = null!;
    public string Title { get; set; } = null!;
    public decimal AmountCad { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? UnitCostCad { get; set; }
    public string? Unit { get; set; }
    public string Justification { get; set; } = null!;

    /// <summary>Enum names as stored by the write side (<c>BudgetSpendType</c> etc.).</summary>
    public string SpendType { get; set; } = null!;
    public string Recurrence { get; set; } = null!;
    public string? Vendor { get; set; }
    public List<string> Tags { get; set; } = [];
    public string Priority { get; set; } = null!;
    public string? Assumptions { get; set; }
    public string? ConsequenceIfUnfunded { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

/// <summary>Maps <see cref="BudgetAllocationReadModel"/> to budgeting.rm_budget_allocations.</summary>
public sealed class BudgetAllocationReadModelConfiguration : IEntityTypeConfiguration<BudgetAllocationReadModel>
{
    public void Configure(EntityTypeBuilder<BudgetAllocationReadModel> builder)
    {
        builder.HasKey(a => a.Id);
        builder.ToTable("rm_budget_allocations", BudgetingServiceCollectionExtensions.SchemaName);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.TenantId).HasColumnName("tenant_id");
        builder.Property(a => a.PeriodId).HasColumnName("period_id");
        builder.Property(a => a.BudgetCodeId).HasColumnName("budget_code_id");
        builder.Property(a => a.Code).HasColumnName("code").HasMaxLength(BudgetCode.CodeMaxLength);
        builder.Property(a => a.AmountCad).HasColumnName("amount_cad").HasPrecision(12, 2);
        builder.Property(a => a.Justification)
            .HasColumnName("justification")
            .HasMaxLength(BudgetAllocation.JustificationMaxLength);
        builder.Property(a => a.Title).HasColumnName("title").HasMaxLength(BudgetAllocation.TitleMaxLength);
        builder.Property(a => a.Quantity).HasColumnName("quantity").HasPrecision(12, 2);
        builder.Property(a => a.UnitCostCad).HasColumnName("unit_cost_cad").HasPrecision(12, 2);
        builder.Property(a => a.Unit).HasColumnName("unit").HasMaxLength(BudgetAllocation.UnitMaxLength);
        builder.Property(a => a.SpendType).HasColumnName("spend_type").HasMaxLength(16);
        builder.Property(a => a.Recurrence).HasColumnName("recurrence").HasMaxLength(16);
        builder.Property(a => a.Vendor).HasColumnName("vendor").HasMaxLength(BudgetAllocation.VendorMaxLength);
        builder.PrimitiveCollection(a => a.Tags).HasColumnName("tags");
        builder.Property(a => a.Priority).HasColumnName("priority").HasMaxLength(16);
        builder.Property(a => a.Assumptions)
            .HasColumnName("assumptions")
            .HasMaxLength(BudgetAllocation.AssumptionsMaxLength);
        builder.Property(a => a.ConsequenceIfUnfunded)
            .HasColumnName("consequence_if_unfunded")
            .HasMaxLength(BudgetAllocation.ConsequenceMaxLength);
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.ModifiedBy).HasColumnName("modified_by");
        builder.Property(a => a.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(a => a.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(a => a.Version).HasColumnName("version");

        // Non-unique, like the write table's: many items per (period, code). With period_id
        // second it is also the per-period scan both read services make.
        builder.HasIndex(a => new { a.TenantId, a.PeriodId, a.BudgetCodeId });
    }
}
