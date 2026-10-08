using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Budgeting.Domain.CostCentres;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>Maps the CostCentre aggregate to budgeting.cost_centres (snake_case columns).</summary>
public sealed class CostCentreConfiguration : IEntityTypeConfiguration<CostCentre>
{
    public void Configure(EntityTypeBuilder<CostCentre> builder)
    {
        builder.ToTable("cost_centres");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.TenantId).HasColumnName("tenant_id");

        builder.Property(c => c.Code).HasColumnName("code").HasMaxLength(CostCentre.CodeMaxLength);
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(CostCentre.NameMaxLength);
        builder.Property(c => c.Description)
            .HasColumnName("description")
            .HasMaxLength(CostCentre.DescriptionMaxLength);

        builder.Property(c => c.OwnerUserId).HasColumnName("owner_user_id");
        builder.Property(c => c.ParentId).HasColumnName("parent_id");
        builder.Property(c => c.IsActive).HasColumnName("is_active");
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.ModifiedBy).HasColumnName("modified_by");
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc");

        // Race backstop for the create handler's duplicate check, and the seek behind every
        // budget-code validation (GetByCodeAsync). Ordinal — the code is stored trimmed, case
        // preserved, exactly as budget_codes.cost_centre holds it.
        builder.HasIndex(c => new { c.TenantId, c.Code }).IsUnique();

        // Serves HasChildrenAsync / HasActiveChildrenAsync. No FK: two cost centres are two
        // aggregates.
        builder.HasIndex(c => new { c.TenantId, c.ParentId });

        // DomainEvents ignore + Version concurrency token come from ModuleDbContext.
    }
}
