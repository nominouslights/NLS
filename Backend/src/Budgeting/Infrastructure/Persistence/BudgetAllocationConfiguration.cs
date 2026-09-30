using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>Maps the BudgetAllocation aggregate to budgeting.budget_allocations (snake_case columns).</summary>
public sealed class BudgetAllocationConfiguration : IEntityTypeConfiguration<BudgetAllocation>
{
    public void Configure(EntityTypeBuilder<BudgetAllocation> builder)
    {
        builder.ToTable("budget_allocations");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(a => a.TenantId).HasColumnName("tenant_id");
        builder.Property(a => a.PeriodId).HasColumnName("period_id");
        builder.Property(a => a.BudgetCodeId).HasColumnName("budget_code_id");

        // Same width as budget_codes.code — this is a copy of that string, and a narrower
        // column would truncate the copy silently.
        builder.Property(a => a.Code)
            .HasColumnName("code")
            .HasMaxLength(BudgetCode.CodeMaxLength);

        // numeric(12,2), the platform's money column (Invoice.TotalCad). The domain caps the
        // value at AmountMax so the column never has to refuse an insert.
        builder.Property(a => a.AmountCad)
            .HasColumnName("amount_cad")
            .HasPrecision(12, 2);

        builder.Property(a => a.Justification)
            .HasColumnName("justification")
            .HasMaxLength(BudgetAllocation.JustificationMaxLength);

        builder.Property(a => a.Title)
            .HasColumnName("title")
            .HasMaxLength(BudgetAllocation.TitleMaxLength);

        // Same numeric(12,2) shape as the amount; the domain rounds both before storing, so the
        // column never silently rounds a value the aggregate computed the amount from.
        builder.Property(a => a.Quantity).HasColumnName("quantity").HasPrecision(12, 2);
        builder.Property(a => a.UnitCostCad).HasColumnName("unit_cost_cad").HasPrecision(12, 2);
        builder.Property(a => a.Unit).HasColumnName("unit").HasMaxLength(BudgetAllocation.UnitMaxLength);

        // Enums as their names, like every budgeting enum column. NO HasDefaultValue in the model,
        // deliberately: EF omits a property from the INSERT when it equals its CLR default, so a
        // model default of ShouldHave on Priority would silently turn every MustHave (value 0)
        // into ShouldHave. The DB-side defaults that backfill existing rows live only in the
        // AddBudgetItemDetails migration.
        builder.Property(a => a.SpendType)
            .HasColumnName("spend_type")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(a => a.Recurrence)
            .HasColumnName("recurrence")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(a => a.Priority)
            .HasColumnName("priority")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(a => a.Vendor).HasColumnName("vendor").HasMaxLength(BudgetAllocation.VendorMaxLength);
        builder.PrimitiveCollection(a => a.Tags).HasColumnName("tags");
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

        // Non-unique: a period holds any number of items per code (a code's budget is the sum of
        // its items). The per-period scan the copy handler and the read side make.
        builder.HasIndex(a => new { a.TenantId, a.PeriodId, a.BudgetCodeId });

        // Both halves of ExistsForCodeAsync, which the delete-code path asks before a hard
        // delete: by id, and by the immutable code string for a code recreated under it.
        builder.HasIndex(a => new { a.TenantId, a.BudgetCodeId });
        builder.HasIndex(a => new { a.TenantId, a.Code });

        // DomainEvents ignore + Version concurrency token come from ModuleDbContext's
        // central aggregate conventions.
    }
}
