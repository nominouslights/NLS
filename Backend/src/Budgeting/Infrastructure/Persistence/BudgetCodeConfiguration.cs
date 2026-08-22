using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>Maps the BudgetCode aggregate to budgeting.budget_codes (snake_case columns).</summary>
public sealed class BudgetCodeConfiguration : IEntityTypeConfiguration<BudgetCode>
{
    public void Configure(EntityTypeBuilder<BudgetCode> builder)
    {
        builder.ToTable("budget_codes");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(c => c.TenantId).HasColumnName("tenant_id");

        builder.Property(c => c.Code)
            .HasColumnName("code")
            .HasMaxLength(BudgetCode.CodeMaxLength);

        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(BudgetCode.NameMaxLength);

        builder.Property(c => c.Description)
            .HasColumnName("description")
            .HasMaxLength(BudgetCode.DescriptionMaxLength);

        builder.Property(c => c.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .HasMaxLength(16);

        // 32 deliberately matches trips.service_type and clients.service_type: the first six
        // members of BudgetServiceLine are the same strings those columns hold, and a report that
        // joins them should not be able to truncate on one side.
        builder.Property(c => c.ServiceLine)
            .HasColumnName("service_line")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(c => c.CostCentre)
            .HasColumnName("cost_centre")
            .HasMaxLength(BudgetCode.CostCentreMaxLength);

        builder.Property(c => c.ParentCodeId).HasColumnName("parent_code_id");

        builder.Property(c => c.GlAccountCode)
            .HasColumnName("gl_account_code")
            .HasMaxLength(BudgetCode.GlAccountCodeMaxLength);

        builder.Property(c => c.TaxTreatment)
            .HasColumnName("tax_treatment")
            .HasConversion<string>()
            .HasMaxLength(24);

        builder.Property(c => c.BudgetOwnerUserId).HasColumnName("budget_owner_user_id");

        builder.Property(c => c.ReviewFrequency)
            .HasColumnName("review_frequency")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(c => c.IsActive).HasColumnName("is_active");
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.ModifiedBy).HasColumnName("modified_by");
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc");

        // Race backstop for the create handler's duplicate check: a double-click can slip two
        // identical codes past the read-then-write check, and this index kills the second insert.
        // Case is not part of the comparison here because Code is stored already upper-cased.
        builder.HasIndex(c => new { c.TenantId, c.Code }).IsUnique();

        // Serves HasChildrenAsync, which both the update and the delete handler call to enforce
        // the one-level hierarchy. No navigation property and no foreign key: two budget codes
        // are two aggregates, and relational links are reserved for entities inside one
        // aggregate boundary.
        builder.HasIndex(c => new { c.TenantId, c.ParentCodeId });

        // DomainEvents ignore + Version concurrency token come from ModuleDbContext's
        // central aggregate conventions.
    }
}
