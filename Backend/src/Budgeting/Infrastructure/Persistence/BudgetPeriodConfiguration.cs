using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>Maps the BudgetPeriod aggregate to budgeting.budget_periods (snake_case columns).</summary>
public sealed class BudgetPeriodConfiguration : IEntityTypeConfiguration<BudgetPeriod>
{
    public void Configure(EntityTypeBuilder<BudgetPeriod> builder)
    {
        builder.ToTable("budget_periods");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(p => p.TenantId).HasColumnName("tenant_id");

        builder.Property(p => p.Granularity)
            .HasColumnName("granularity")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(p => p.Year).HasColumnName("year");
        builder.Property(p => p.Ordinal).HasColumnName("ordinal");
        builder.Property(p => p.StartsOn).HasColumnName("starts_on");
        builder.Property(p => p.EndsOn).HasColumnName("ends_on");
        builder.Property(p => p.Label).HasColumnName("label").HasMaxLength(BudgetPeriod.LabelMaxLength);

        builder.Property(p => p.State)
            .HasColumnName("state")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(p => p.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(p => p.UpdatedAtUtc).HasColumnName("updated_at_utc");

        // Race backstop for the handler's overlap check: a double-click can slip two
        // identical periods past the read-then-write check, and this index kills the
        // second insert. Cross-granularity overlaps (a month inside a quarter) are the
        // handler's job — no index can express that without an exclusion constraint.
        builder.HasIndex(p => new { p.TenantId, p.Granularity, p.Year, p.Ordinal }).IsUnique();

        builder.HasIndex(p => new { p.TenantId, p.StartsOn });

        // DomainEvents ignore + Version concurrency token come from ModuleDbContext's
        // central aggregate conventions.
    }
}
