using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Budgeting.Domain.Actuals;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>Maps the ActualTransaction aggregate to budgeting.actual_transactions (snake_case columns).</summary>
public sealed class ActualTransactionConfiguration : IEntityTypeConfiguration<ActualTransaction>
{
    public void Configure(EntityTypeBuilder<ActualTransaction> builder)
    {
        builder.ToTable("actual_transactions");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(t => t.TenantId).HasColumnName("tenant_id");
        builder.Property(t => t.BudgetCodeId).HasColumnName("budget_code_id");

        // Same width as budget_codes.code — this is a copy of that string, and a narrower
        // column would truncate the copy silently.
        builder.Property(t => t.Code)
            .HasColumnName("code")
            .HasMaxLength(BudgetCode.CodeMaxLength);

        // numeric(12,2), the platform's money column (Invoice.TotalCad, BudgetAllocation.AmountCad).
        // The domain caps the value at AmountMax so the column never has to refuse an insert.
        builder.Property(t => t.AmountCad)
            .HasColumnName("amount_cad")
            .HasPrecision(12, 2);

        // There is no period_id column, and its absence is the design: the period is derived from
        // this date. See ActualTransaction.
        builder.Property(t => t.OccurredOn).HasColumnName("occurred_on");

        builder.Property(t => t.Description)
            .HasColumnName("description")
            .HasMaxLength(ActualTransaction.DescriptionMaxLength);

        builder.Property(t => t.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(t => t.ExternalReference)
            .HasColumnName("external_reference")
            .HasMaxLength(ActualTransaction.ExternalReferenceMaxLength);

        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.ModifiedBy).HasColumnName("modified_by");
        builder.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(t => t.UpdatedAtUtc).HasColumnName("updated_at_utc");

        // The period read path: "this period's transactions" is a range scan on occurred_on,
        // because nothing stores a period id.
        builder.HasIndex(t => new { t.TenantId, t.OccurredOn });

        // Half of ExistsForCodeAsync, which the delete-code path asks before a hard delete. The
        // other half rides the (tenant_id, occurred_on) index's table rather than one of its own:
        // matching on the code string alone is the rare recreated-code case, and the rollup never
        // needs it. Add (tenant_id, code) if that ever shows up in a plan.
        builder.HasIndex(t => new { t.TenantId, t.BudgetCodeId });

        // The re-import guard. Unique per (tenant, source) and only where a reference is set, so
        // the thousands of hand-entered rows — all null — never collide with each other while a
        // second pull of the same QuickBooks transaction is refused by the database rather than
        // silently doubling a figure on the report. The filter is what makes both true at once:
        // Postgres treats NULLs as distinct in a plain unique index, but the partial index also
        // keeps the index off every row that could never participate.
        builder.HasIndex(t => new { t.TenantId, t.Source, t.ExternalReference })
            .IsUnique()
            .HasFilter("external_reference IS NOT NULL");

        // DomainEvents ignore + Version concurrency token come from ModuleDbContext's
        // central aggregate conventions.
    }
}
