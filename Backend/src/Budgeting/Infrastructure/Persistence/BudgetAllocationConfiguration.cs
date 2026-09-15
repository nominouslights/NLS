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

        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.ModifiedBy).HasColumnName("modified_by");
        builder.Property(a => a.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(a => a.UpdatedAtUtc).HasColumnName("updated_at_utc");

        // One line per (period, code) is the upsert rule; the set handler enforces it with a
        // read-then-write, and this index is the race backstop that kills the second insert
        // when two first-time sets collide. Also the lookup path for GetAsync.
        builder.HasIndex(a => new { a.TenantId, a.PeriodId, a.BudgetCodeId }).IsUnique();

        // Both halves of ExistsForCodeAsync, which the delete-code path asks before a hard
        // delete: by id, and by the immutable code string for a code recreated under it.
        builder.HasIndex(a => new { a.TenantId, a.BudgetCodeId });
        builder.HasIndex(a => new { a.TenantId, a.Code });

        // DomainEvents ignore + Version concurrency token come from ModuleDbContext's
        // central aggregate conventions.
    }
}
