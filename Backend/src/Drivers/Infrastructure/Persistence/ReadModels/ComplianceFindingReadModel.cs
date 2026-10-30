using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Drivers.Infrastructure.Persistence.ReadModels;

/// <summary>Read-side projection of a <c>ComplianceFinding</c> into <c>drivers.rm_compliance_findings</c>, maintained by <c>ComplianceFindingProjection</c>.</summary>
public sealed class ComplianceFindingReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DriverId { get; set; }
    public DateOnly LocalDate { get; set; }
    public string Rule { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string Source { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? FollowUpNote { get; set; }
    public DateTimeOffset OpenedAtUtc { get; set; }
    public Guid OpenedByUserId { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public int Version { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class ComplianceFindingReadModelConfiguration : IEntityTypeConfiguration<ComplianceFindingReadModel>
{
    public void Configure(EntityTypeBuilder<ComplianceFindingReadModel> builder)
    {
        builder.HasKey(f => f.Id);
        builder.ToTable("rm_compliance_findings", DriversServiceCollectionExtensions.SchemaName);

        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.TenantId).HasColumnName("tenant_id");
        builder.Property(f => f.DriverId).HasColumnName("driver_id");
        builder.Property(f => f.LocalDate).HasColumnName("local_date");
        builder.Property(f => f.Rule).HasColumnName("rule").HasMaxLength(64);
        builder.Property(f => f.Severity).HasColumnName("severity").HasMaxLength(16);
        builder.Property(f => f.Status).HasColumnName("status").HasMaxLength(16);
        builder.Property(f => f.Source).HasColumnName("source").HasMaxLength(16);
        builder.Property(f => f.Description).HasColumnName("description");
        builder.Property(f => f.FollowUpNote).HasColumnName("follow_up_note");
        builder.Property(f => f.OpenedAtUtc).HasColumnName("opened_at_utc");
        builder.Property(f => f.OpenedByUserId).HasColumnName("opened_by_user_id");
        builder.Property(f => f.ClosedAtUtc).HasColumnName("closed_at_utc");
        builder.Property(f => f.ClosedByUserId).HasColumnName("closed_by_user_id");
        builder.Property(f => f.Version).HasColumnName("version");
        builder.Property(f => f.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(f => new { f.TenantId, f.DriverId, f.LocalDate });
    }
}
