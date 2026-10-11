using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Compliance.Domain.Numbering;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Compliance.Domain.RegulatoryBasis;
using NorthernLink.Compliance.Domain.RuleSets;

namespace NorthernLink.Compliance.Infrastructure.Persistence;

/// <summary>Maps the ComplianceRecord aggregate to compliance.compliance_records (snake_case columns; collections as jsonb).</summary>
public sealed class ComplianceRecordConfiguration : IEntityTypeConfiguration<ComplianceRecord>
{
    /// <summary>The unique (tenant_id, source_module, source_record_id) index — the registration idempotency backstop.</summary>
    public const string SourceIndex = "IX_compliance_records_tenant_id_source_module_source_record_id";

    public void Configure(EntityTypeBuilder<ComplianceRecord> builder)
    {
        builder.ToTable("compliance_records");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.TenantId).HasColumnName("tenant_id");
        builder.Property(r => r.RecordType).HasColumnName("record_type").HasConversion<string>().HasMaxLength(8);
        builder.Property(r => r.DocumentNumber).HasColumnName("document_number").HasMaxLength(DocumentNumberFormat.MaxLength);
        builder.Property(r => r.Title).HasColumnName("title").HasMaxLength(ComplianceRecord.TitleMaxLength);
        builder.Property(r => r.SourceModule).HasColumnName("source_module").HasMaxLength(ComplianceRecord.SourceModuleMaxLength);
        builder.Property(r => r.SourceRecordId).HasColumnName("source_record_id").HasMaxLength(ComplianceRecord.SourceRecordIdMaxLength);
        builder.Property(r => r.AnchorDates).HasColumnName("anchor_dates").HasJsonb();
        builder.Property(r => r.RetainUntil).HasColumnName("retain_until");
        builder.Property(r => r.RetentionPending).HasColumnName("retention_pending");
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(24);
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
        builder.Ignore(r => r.IsHeld);

        // The subject is three columns on the same row, not a table of its own.
        builder.OwnsOne(r => r.Subject, subject =>
        {
            subject.Property(s => s.Kind).HasColumnName("subject_kind").HasConversion<string>().HasMaxLength(16);
            subject.Property(s => s.Id).HasColumnName("subject_id");
            subject.Property(s => s.Display).HasColumnName("subject_display").HasMaxLength(RecordSubject.DisplayMaxLength);
        });
        builder.Navigation(r => r.Subject).IsRequired();

        // Registration idempotency backstop (see RecordRegistrar) and the lookups the handlers make.
        builder.HasIndex(r => new { r.TenantId, r.SourceModule, r.SourceRecordId }).IsUnique().HasDatabaseName(SourceIndex);
        builder.HasIndex(r => new { r.TenantId, r.DocumentNumber }).IsUnique();
        builder.HasIndex(r => new { r.TenantId, r.Status, r.RetainUntil });

        // DomainEvents ignore + Version concurrency token come from ModuleDbContext's
        // central aggregate conventions.
    }
}

/// <summary>Maps DocumentSeries to compliance.document_series — one row per (tenant, series, year).</summary>
public sealed class DocumentSeriesConfiguration : IEntityTypeConfiguration<DocumentSeries>
{
    public void Configure(EntityTypeBuilder<DocumentSeries> builder)
    {
        builder.ToTable("document_series");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.TenantId).HasColumnName("tenant_id");
        builder.Property(s => s.Series).HasColumnName("series").HasConversion<string>().HasMaxLength(4);
        builder.Property(s => s.Year).HasColumnName("year");
        builder.Property(s => s.LastSequence).HasColumnName("last_sequence");
        builder.Property(s => s.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(s => new { s.TenantId, s.Series, s.Year }).IsUnique();
    }
}

/// <summary>Maps TenantComplianceSettings to compliance.tenant_compliance_settings — one row per tenant.</summary>
public sealed class TenantComplianceSettingsConfiguration : IEntityTypeConfiguration<TenantComplianceSettings>
{
    public void Configure(EntityTypeBuilder<TenantComplianceSettings> builder)
    {
        builder.ToTable("tenant_compliance_settings");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.TenantId).HasColumnName("tenant_id");
        builder.Property(s => s.CarrierLegalName).HasColumnName("carrier_legal_name").HasMaxLength(TenantComplianceSettings.CarrierLegalNameMaxLength);
        builder.Property(s => s.NscNumber).HasColumnName("nsc_number").HasMaxLength(TenantComplianceSettings.NscNumberMaxLength);
        builder.Property(s => s.DocumentPrefix).HasColumnName("document_prefix").HasMaxLength(TenantComplianceSettings.DocumentPrefixMaxLength);
        builder.Property(s => s.PrincipalPlaceOfBusiness).HasColumnName("principal_place_of_business").HasMaxLength(TenantComplianceSettings.PrincipalPlaceOfBusinessMaxLength);
        builder.Property(s => s.FileLocationNote).HasColumnName("file_location_note").HasMaxLength(TenantComplianceSettings.FileLocationNoteMaxLength);
        builder.Property(s => s.ModifiedBy).HasColumnName("modified_by");
        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(s => s.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(s => s.TenantId).IsUnique();
    }
}

/// <summary>Maps TenantRuleSetApplication to compliance.tenant_rule_set_applications (append-only).</summary>
public sealed class TenantRuleSetApplicationConfiguration : IEntityTypeConfiguration<TenantRuleSetApplication>
{
    public void Configure(EntityTypeBuilder<TenantRuleSetApplication> builder)
    {
        builder.ToTable("tenant_rule_set_applications");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.TenantId).HasColumnName("tenant_id");
        builder.Property(a => a.RuleSetId).HasColumnName("rule_set_id");
        builder.Property(a => a.AppliedAtUtc).HasColumnName("applied_at_utc");
        builder.Property(a => a.ChangeNotice).HasColumnName("change_notice").HasMaxLength(TenantRuleSetApplication.ChangeNoticeMaxLength);
        builder.Property(a => a.AppliedBy).HasColumnName("applied_by");

        builder.HasIndex(a => new { a.TenantId, a.AppliedAtUtc });
    }
}

/// <summary>Maps DestructionLogEntry to compliance.destruction_log (plain, append-only, tenant-scoped).</summary>
public sealed class DestructionLogEntryConfiguration : IEntityTypeConfiguration<DestructionLogEntry>
{
    public void Configure(EntityTypeBuilder<DestructionLogEntry> builder)
    {
        builder.ToTable("destruction_log");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(d => d.TenantId).HasColumnName("tenant_id");
        builder.Property(d => d.RecordId).HasColumnName("record_id");
        builder.Property(d => d.DocumentNumber).HasColumnName("document_number").HasMaxLength(DocumentNumberFormat.MaxLength);
        builder.Property(d => d.RecordType).HasColumnName("record_type").HasConversion<string>().HasMaxLength(8);
        builder.Property(d => d.DisposedAtUtc).HasColumnName("disposed_at_utc");
        builder.Property(d => d.DisposedBy).HasColumnName("disposed_by");
        builder.Property(d => d.Method).HasColumnName("method").HasMaxLength(DestructionLogEntry.MethodMaxLength);
        builder.Property(d => d.Note).HasColumnName("note").HasMaxLength(DestructionLogEntry.NoteMaxLength);

        builder.HasIndex(d => new { d.TenantId, d.DisposedAtUtc });
    }
}

/// <summary>Maps RuleSet to compliance.rule_sets — reference data, one jsonb column per typed section.</summary>
public sealed class RuleSetConfiguration : IEntityTypeConfiguration<RuleSet>
{
    public void Configure(EntityTypeBuilder<RuleSet> builder)
    {
        builder.ToTable("rule_sets");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.Jurisdiction).HasColumnName("jurisdiction").HasMaxLength(RuleSet.JurisdictionMaxLength);
        builder.Property(r => r.Version).HasColumnName("version");
        builder.Property(r => r.EffectiveFrom).HasColumnName("effective_from");
        builder.Property(r => r.PublishedAtUtc).HasColumnName("published_at_utc");
        builder.Property(r => r.Hos).HasColumnName("hos").HasJsonb();
        builder.Property(r => r.Retention).HasColumnName("retention").HasJsonb();
        builder.Property(r => r.Inspection).HasColumnName("inspection").HasJsonb();
        builder.Property(r => r.DriverQualification).HasColumnName("driver_qualification").HasJsonb();
        builder.Property(r => r.Filing).HasColumnName("filing").HasJsonb();
        builder.Property(r => r.Thresholds).HasColumnName("thresholds").HasJsonb();
        builder.Property(r => r.Insurance).HasColumnName("insurance").HasJsonb();

        builder.HasIndex(r => new { r.Jurisdiction, r.Version }).IsUnique();
    }
}

/// <summary>Maps RegulatoryBasis to compliance.regulatory_basis — reference data keyed by the stable basis key.</summary>
public sealed class RegulatoryBasisConfiguration : IEntityTypeConfiguration<RegulatoryBasis>
{
    public void Configure(EntityTypeBuilder<RegulatoryBasis> builder)
    {
        builder.ToTable("regulatory_basis");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(b => b.Key).HasColumnName("key").HasMaxLength(RegulatoryBasis.KeyMaxLength);
        builder.Property(b => b.Jurisdiction).HasColumnName("jurisdiction").HasMaxLength(RegulatoryBasis.JurisdictionMaxLength);
        builder.Property(b => b.Instrument).HasColumnName("instrument").HasConversion<string>().HasMaxLength(24);
        builder.Property(b => b.Citation).HasColumnName("citation").HasMaxLength(RegulatoryBasis.CitationMaxLength);
        builder.Property(b => b.Title).HasColumnName("title").HasMaxLength(RegulatoryBasis.TitleMaxLength);
        builder.Property(b => b.Quotation).HasColumnName("quotation");
        builder.Property(b => b.PlainExplanation).HasColumnName("plain_explanation");
        builder.Property(b => b.Consequence).HasColumnName("consequence");
        builder.Property(b => b.SourceUrl).HasColumnName("source_url").HasMaxLength(1024);
        builder.Property(b => b.PlanCitationNote).HasColumnName("plan_citation_note");
        builder.Property(b => b.EffectiveFrom).HasColumnName("effective_from");
        builder.Property(b => b.RuleSetVersion).HasColumnName("rule_set_version");

        builder.HasIndex(b => b.Key).IsUnique();
        builder.HasIndex(b => b.Instrument);
    }
}
