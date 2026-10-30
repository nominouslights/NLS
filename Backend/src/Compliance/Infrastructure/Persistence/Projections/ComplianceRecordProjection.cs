using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Compliance.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;

namespace NorthernLink.Compliance.Infrastructure.Persistence.Projections;

/// <summary>Projects <see cref="ComplianceRecord"/> into <c>compliance.rm_compliance_records</c>.</summary>
internal sealed class ComplianceRecordProjection : ComplianceProjection<ComplianceRecord, ComplianceRecordReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(ComplianceRecord));

    protected override void Map(ComplianceRecord source, ComplianceRecordReadModel row)
    {
        var definition = ComplianceRecordCatalogue.For(source.RecordType);

        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.RecordType = source.RecordType.ToString();
        row.DocumentNumber = source.DocumentNumber;
        row.Title = source.Title;
        row.SubjectKind = source.Subject.Kind.ToString();
        row.SubjectId = source.Subject.Id;
        row.SubjectDisplay = source.Subject.Display;
        row.SourceModule = source.SourceModule;
        row.SourceRecordId = source.SourceRecordId;
        row.AnchorDates = source.AnchorDates.ToDictionary(a => a.Key.ToString(), a => a.Value);
        row.RetainUntil = source.RetainUntil;
        row.RetentionPending = source.RetentionPending;
        row.IsPermanent = definition.Rule.IsPermanent;
        row.Status = source.Status.ToString();
        row.IsHeld = source.IsHeld;
        row.Holds = source.Holds;
        row.SupportingLinks = source.SupportingLinks;
        row.PrintTemplateKey = source.PrintTemplateKey;
        row.DocumentVersion = source.DocumentVersion;
        row.SupersedesId = source.SupersedesId;
        row.RegulatoryBasisKeys = source.RegulatoryBasisKeys;
        row.RegisteredBy = source.RegisteredBy;
        row.RegisteredAtUtc = source.RegisteredAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.DisposedAtUtc = source.DisposedAtUtc;
        row.Version = source.Version;
    }
}
