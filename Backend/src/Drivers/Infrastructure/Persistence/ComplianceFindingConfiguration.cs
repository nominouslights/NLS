using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Drivers.Domain.Hos;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>Maps the ComplianceFinding aggregate to drivers.compliance_findings (snake_case columns; enums as their names).</summary>
public sealed class ComplianceFindingConfiguration : IEntityTypeConfiguration<ComplianceFinding>
{
    public void Configure(EntityTypeBuilder<ComplianceFinding> builder)
    {
        builder.ToTable("compliance_findings");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(f => f.TenantId).HasColumnName("tenant_id");
        builder.Property(f => f.DriverId).HasColumnName("driver_id");
        builder.Property(f => f.LocalDate).HasColumnName("local_date");
        builder.Property(f => f.Rule).HasColumnName("rule").HasMaxLength(ComplianceFinding.RuleMaxLength);
        builder.Property(f => f.Severity).HasColumnName("severity").HasConversion<string>().HasMaxLength(16);
        builder.Property(f => f.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        builder.Property(f => f.Description).HasColumnName("description").HasMaxLength(ComplianceFinding.DescriptionMaxLength);
        builder.Property(f => f.FollowUpNote).HasColumnName("follow_up_note").HasMaxLength(ComplianceFinding.FollowUpNoteMaxLength);
        builder.Property(f => f.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(16);
        builder.Property(f => f.OpenedAtUtc).HasColumnName("opened_at_utc");
        builder.Property(f => f.OpenedByUserId).HasColumnName("opened_by_user_id");
        builder.Property(f => f.ClosedAtUtc).HasColumnName("closed_at_utc");
        builder.Property(f => f.ClosedByUserId).HasColumnName("closed_by_user_id");
        builder.Property(f => f.UpdatedAtUtc).HasColumnName("updated_at_utc");

        // The audit asks "findings for this driver on these days".
        builder.HasIndex(f => new { f.TenantId, f.DriverId, f.LocalDate });
    }
}
