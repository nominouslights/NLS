using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Budgeting.Domain.Actuals;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a recorded transaction into <c>budgeting.rm_actual_transactions</c> —
/// the rows the period view's actuals list renders and the rollup's spend column is summed from.
/// A mutable class rather than a record, like every read model on the platform (see
/// <see cref="BudgetCodeReadModel"/> for why); the wire contract is
/// <see cref="Application.Actuals.ActualTransactionResponse"/>.
/// <para>
/// <b>No code name, category or service line here</b>, for the same reason
/// <see cref="BudgetAllocationReadModel"/> carries none: those describe the code, not the
/// transaction, and the projection base only ever reads its own source aggregate — a category
/// copied onto this row would go stale the moment a planner re-classified the code, and the
/// rollup would quietly disagree with the lines it is built from. The read services resolve them
/// from <c>rm_budget_codes</c> on every read instead.
/// </para>
/// <para>
/// <b>And no period id</b> — not as a denormalization either. The period is derived from
/// <see cref="OccurredOn"/> everywhere, including here; see <see cref="ActualTransaction"/>.
/// </para>
/// </summary>
public sealed class ActualTransactionReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BudgetCodeId { get; set; }
    public string Code { get; set; } = null!;
    public decimal AmountCad { get; set; }
    public DateOnly OccurredOn { get; set; }
    public string Description { get; set; } = null!;
    public string Source { get; set; } = null!;
    public string? ExternalReference { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

/// <summary>Maps <see cref="ActualTransactionReadModel"/> to budgeting.rm_actual_transactions.</summary>
public sealed class ActualTransactionReadModelConfiguration : IEntityTypeConfiguration<ActualTransactionReadModel>
{
    public void Configure(EntityTypeBuilder<ActualTransactionReadModel> builder)
    {
        builder.HasKey(t => t.Id);
        builder.ToTable("rm_actual_transactions", BudgetingServiceCollectionExtensions.SchemaName);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.TenantId).HasColumnName("tenant_id");
        builder.Property(t => t.BudgetCodeId).HasColumnName("budget_code_id");
        builder.Property(t => t.Code).HasColumnName("code").HasMaxLength(BudgetCode.CodeMaxLength);
        builder.Property(t => t.AmountCad).HasColumnName("amount_cad").HasPrecision(12, 2);
        builder.Property(t => t.OccurredOn).HasColumnName("occurred_on");
        builder.Property(t => t.Description)
            .HasColumnName("description")
            .HasMaxLength(ActualTransaction.DescriptionMaxLength);
        builder.Property(t => t.Source).HasColumnName("source").HasMaxLength(16);
        builder.Property(t => t.ExternalReference)
            .HasColumnName("external_reference")
            .HasMaxLength(ActualTransaction.ExternalReferenceMaxLength);
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.ModifiedBy).HasColumnName("modified_by");
        builder.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(t => t.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(t => t.Version).HasColumnName("version");

        // Both reads are per-period, which means per date range: the actuals list and the
        // rollup's spend column. Code second serves the rollup's grouping off the same index.
        builder.HasIndex(t => new { t.TenantId, t.OccurredOn });
        builder.HasIndex(t => new { t.TenantId, t.BudgetCodeId });
    }
}
