using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Compliance.Domain.Numbering;
using NorthernLink.Compliance.Domain.Records;

namespace NorthernLink.Compliance.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a compliance record into <c>compliance.rm_compliance_records</c> —
/// the row the registry lists and filters. A mutable class rather than a record, like every read
/// model on the platform; the wire contract is <c>ComplianceRecordResponse</c>. Enums are stored
/// as their names so the table reads like the API. <see cref="IsHeld"/> and
/// <see cref="IsPermanent"/> are denormalized so the due-for-disposal query is one WHERE.
/// </summary>
public sealed class ComplianceRecordReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string RecordType { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string SubjectKind { get; set; } = null!;
    public Guid? SubjectId { get; set; }
    public string SubjectDisplay { get; set; } = null!;
    public string SourceModule { get; set; } = null!;
    public string SourceRecordId { get; set; } = null!;
    public IReadOnlyDictionary<string, DateOnly> AnchorDates { get; set; } = new Dictionary<string, DateOnly>();
    public DateOnly? RetainUntil { get; set; }
    public bool RetentionPending { get; set; }
    public bool IsPermanent { get; set; }
    public string Status { get; set; } = null!;
    public bool IsHeld { get; set; }
    public IReadOnlyList<RecordHold> Holds { get; set; } = [];
    public IReadOnlyList<SupportingLink> SupportingLinks { get; set; } = [];
    public string PrintTemplateKey { get; set; } = null!;
    public int DocumentVersion { get; set; }
    public Guid? SupersedesId { get; set; }
    public IReadOnlyList<string> RegulatoryBasisKeys { get; set; } = [];
    public Guid? RegisteredBy { get; set; }
    public DateTimeOffset RegisteredAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? DisposedAtUtc { get; set; }
    public int Version { get; set; }
}

/// <summary>Maps <see cref="ComplianceRecordReadModel"/> to compliance.rm_compliance_records.</summary>
public sealed class ComplianceRecordReadModelConfiguration : IEntityTypeConfiguration<ComplianceRecordReadModel>
{
    public void Configure(EntityTypeBuilder<ComplianceRecordReadModel> builder)
    {
        builder.HasKey(r => r.Id);
        builder.ToTable("rm_compliance_records", ComplianceServiceCollectionExtensions.SchemaName);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id");
        builder.Property(r => r.RecordType).HasColumnName("record_type").HasMaxLength(8);
        builder.Property(r => r.DocumentNumber).HasColumnName("document_number").HasMaxLength(DocumentNumberFormat.MaxLength);
        builder.Property(r => r.Title).HasColumnName("title").HasMaxLength(ComplianceRecord.TitleMaxLength);
        builder.Property(r => r.SubjectKind).HasColumnName("subject_kind").HasMaxLength(16);
        builder.Property(r => r.SubjectId).HasColumnName("subject_id");
        builder.Property(r => r.SubjectDisplay).HasColumnName("subject_display").HasMaxLength(RecordSubject.DisplayMaxLength);
        builder.Property(r => r.SourceModule).HasColumnName("source_module").HasMaxLength(ComplianceRecord.SourceModuleMaxLength);
        builder.Property(r => r.SourceRecordId).HasColumnName("source_record_id").HasMaxLength(ComplianceRecord.SourceRecordIdMaxLength);
        builder.Property(r => r.AnchorDates).HasColumnName("anchor_dates").HasJsonb();
        builder.Property(r => r.RetainUntil).HasColumnName("retain_until");
        builder.Property(r => r.RetentionPending).HasColumnName("retention_pending");
        builder.Property(r => r.IsPermanent).HasColumnName("is_permanent");
        builder.Property(r => r.Status).HasColumnName("status").HasMaxLength(24);
        builder.Property(r => r.IsHeld).HasColumnName("is_held");
        builder.Property(r => r.Holds).HasColumnName("holds").HasJsonb();
        builder.Property(r => r.SupportingLinks).HasColumnName("supporting_links").HasJsonb();
        builder.Property(r => r.PrintTemplateKey).HasColumnName("print_template_key").HasMaxLength(ComplianceRecord.PrintTemplateKeyMaxLength);
        builder.Property(r => r.DocumentVersion).HasColumnName("document_version");
        builder.Property(r => r.SupersedesId).HasColumnName("supersedes_id");
        builder.Property(r => r.RegulatoryBasisKeys).HasColumnName("regulatory_basis_keys").HasJsonb();
        builder.Property(r => r.RegisteredBy).HasColumnName("registered_by");
        builder.Property(r => r.RegisteredAtUtc).HasColumnName("registered_at_utc");
        builder.Property(r => r.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(r => r.DisposedAtUtc).HasColumnName("disposed_at_utc");
        builder.Property(r => r.Version).HasColumnName("version");

        // The list screen's filters: by type, by subject, by status/due date.
        builder.HasIndex(r => new { r.TenantId, r.RecordType });
        builder.HasIndex(r => new { r.TenantId, r.SubjectKind, r.SubjectId });
        builder.HasIndex(r => new { r.TenantId, r.Status, r.RetainUntil });
    }
}
