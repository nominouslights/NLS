using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a budget code into <c>budgeting.rm_budget_codes</c> — the row the
/// Budgeting console's Budget Codes screen lists. Enum values are projected as their PascalCase
/// names.
/// <para>
/// A mutable class rather than a record, like every read model on the platform: the projection
/// worker constructs one with <c>new TRead()</c> and re-applies <c>Map</c> onto the EF-tracked
/// row on every update, and a record's value equality would make two states of the same tracked
/// entity compare unequal. The immutable shape in this slice is
/// <see cref="Application.Codes.BudgetCodeResponse"/> — the wire contract, which is a record.
/// </para>
/// <para>
/// <b>No owner or parent display columns.</b> The parent's code and the owner's email are
/// resolved in the read service, not stored here: the projection base only ever reads its own
/// source aggregate, so a denormalized email would go stale the day Identity grows a rename and
/// nothing would notice.
/// </para>
/// </summary>
public sealed class BudgetCodeReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Category { get; set; } = null!;
    public string? ServiceLine { get; set; }
    public string? CostCentre { get; set; }
    public Guid? ParentCodeId { get; set; }
    public string? GlAccountCode { get; set; }
    public string? TaxTreatment { get; set; }
    public Guid? BudgetOwnerUserId { get; set; }
    public string ReviewFrequency { get; set; } = null!;
    public bool IsActive { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

/// <summary>Maps <see cref="BudgetCodeReadModel"/> to budgeting.rm_budget_codes.</summary>
public sealed class BudgetCodeReadModelConfiguration : IEntityTypeConfiguration<BudgetCodeReadModel>
{
    public void Configure(EntityTypeBuilder<BudgetCodeReadModel> builder)
    {
        builder.HasKey(c => c.Id);
        builder.ToTable("rm_budget_codes", BudgetingServiceCollectionExtensions.SchemaName);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.Code).HasColumnName("code").HasMaxLength(BudgetCode.CodeMaxLength);
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(BudgetCode.NameMaxLength);
        builder.Property(c => c.Description)
            .HasColumnName("description")
            .HasMaxLength(BudgetCode.DescriptionMaxLength);
        builder.Property(c => c.Category).HasColumnName("category").HasMaxLength(16);
        builder.Property(c => c.ServiceLine).HasColumnName("service_line").HasMaxLength(32);
        builder.Property(c => c.CostCentre)
            .HasColumnName("cost_centre")
            .HasMaxLength(BudgetCode.CostCentreMaxLength);
        builder.Property(c => c.ParentCodeId).HasColumnName("parent_code_id");
        builder.Property(c => c.GlAccountCode)
            .HasColumnName("gl_account_code")
            .HasMaxLength(BudgetCode.GlAccountCodeMaxLength);
        builder.Property(c => c.TaxTreatment).HasColumnName("tax_treatment").HasMaxLength(24);
        builder.Property(c => c.BudgetOwnerUserId).HasColumnName("budget_owner_user_id");
        builder.Property(c => c.ReviewFrequency).HasColumnName("review_frequency").HasMaxLength(16);
        builder.Property(c => c.IsActive).HasColumnName("is_active");
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.ModifiedBy).HasColumnName("modified_by");
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(c => c.Version).HasColumnName("version");

        // The screen's default ordering, and the lookup a future allocations slice will make
        // when it resolves an allocation's code string back to its row.
        builder.HasIndex(c => new { c.TenantId, c.Code }).IsUnique();

        // Forward-looking, like the unique index above: Stage 6.2's revenue-mix report groups the
        // read side by service line, and that is the only query this index exists for.
        builder.HasIndex(c => new { c.TenantId, c.ServiceLine });
    }
}
