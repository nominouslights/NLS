using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a budget period into <c>budgeting.rm_budget_periods</c> —
/// the row the Budgeting console's periods screen lists. Enum values are projected as
/// their PascalCase names.
/// </summary>
public sealed class BudgetPeriodReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Label { get; set; } = null!;
    public string Granularity { get; set; } = null!;
    public int Year { get; set; }
    public int Ordinal { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public string State { get; set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

/// <summary>Maps <see cref="BudgetPeriodReadModel"/> to budgeting.rm_budget_periods.</summary>
public sealed class BudgetPeriodReadModelConfiguration : IEntityTypeConfiguration<BudgetPeriodReadModel>
{
    public void Configure(EntityTypeBuilder<BudgetPeriodReadModel> builder)
    {
        builder.HasKey(p => p.Id);
        builder.ToTable("rm_budget_periods", BudgetingServiceCollectionExtensions.SchemaName);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.TenantId).HasColumnName("tenant_id");
        builder.Property(p => p.Label).HasColumnName("label");
        builder.Property(p => p.Granularity).HasColumnName("granularity");
        builder.Property(p => p.Year).HasColumnName("year");
        builder.Property(p => p.Ordinal).HasColumnName("ordinal");
        builder.Property(p => p.StartsOn).HasColumnName("starts_on");
        builder.Property(p => p.EndsOn).HasColumnName("ends_on");
        builder.Property(p => p.State).HasColumnName("state");
        builder.Property(p => p.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(p => p.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(p => p.Version).HasColumnName("version");
    }
}
