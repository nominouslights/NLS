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
    public decimal AmountCad { get; set; }
    public string Justification { get; set; } = null!;
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
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.ModifiedBy).HasColumnName("modified_by");
        builder.Property(a => a.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(a => a.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(a => a.Version).HasColumnName("version");

        // Mirrors the write table's uniqueness, and with period_id second it is also the
        // per-period scan both read services make.
        builder.HasIndex(a => new { a.TenantId, a.PeriodId, a.BudgetCodeId }).IsUnique();
    }
}
