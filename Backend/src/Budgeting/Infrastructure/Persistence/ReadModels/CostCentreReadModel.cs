using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Budgeting.Domain.CostCentres;

namespace NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a cost centre into <c>budgeting.rm_cost_centres</c>. A mutable class,
/// like every read model on the platform (see <see cref="BudgetCodeReadModel"/>). No owner or
/// parent display columns: those are resolved by the read service on every read, so they cannot
/// go stale.
/// </summary>
public sealed class CostCentreReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? OwnerUserId { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

/// <summary>Maps <see cref="CostCentreReadModel"/> to budgeting.rm_cost_centres.</summary>
public sealed class CostCentreReadModelConfiguration : IEntityTypeConfiguration<CostCentreReadModel>
{
    public void Configure(EntityTypeBuilder<CostCentreReadModel> builder)
    {
        builder.HasKey(c => c.Id);
        builder.ToTable("rm_cost_centres", BudgetingServiceCollectionExtensions.SchemaName);

        builder.Property(c => c.Id).HasColumnName("id");
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
        builder.Property(c => c.Version).HasColumnName("version");

        // One code per tenant, like the write table; also serves the ordered register read.
        builder.HasIndex(c => new { c.TenantId, c.Code }).IsUnique();
    }
}
