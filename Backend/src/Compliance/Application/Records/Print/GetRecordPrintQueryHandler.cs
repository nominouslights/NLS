using System.Globalization;
using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Application.Printing;
using NorthernLink.Compliance.Domain.Numbering;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Records.Print;

/// <summary>
/// Handles <see cref="GetRecordPrintQuery"/> — NL-SP-02 s. 11; HTA s. 318.8(2) and NSC 15 s.
/// 3(d)(i): records must be producible at the principal place of business during an audit, so
/// every registered record prints with its number, retention and the rules it exists for. Reads
/// the aggregate (not the read model) so a just-registered record prints immediately.
/// </summary>
public sealed class GetRecordPrintQueryHandler(
    IComplianceRecordRepository records,
    ITenantComplianceSettingsRepository settings,
    IRegulatoryBasisRepository basisRepository,
    IRecordPrintService printService)
    : IQueryHandler<GetRecordPrintQuery, RecordPrintResult>
{
    public async Task<Result<RecordPrintResult>> Handle(GetRecordPrintQuery query, CancellationToken cancellationToken)
    {
        var record = await records.GetByIdAsync(query.RecordId, cancellationToken);
        if (record is null)
        {
            return Result.Failure<RecordPrintResult>(ComplianceRecordErrors.NotFound);
        }

        var tenantSettings = await settings.GetAsync(cancellationToken);
        var definition = ComplianceRecordCatalogue.For(record.RecordType);
        var basis = await basisRepository.GetByKeysAsync(record.RegulatoryBasisKeys, cancellationToken);

        var document = BuildDocument(record, definition, tenantSettings, basis, DateTimeOffset.UtcNow);
        var bytes = printService.Render(document);
        return Result.Success(new RecordPrintResult(bytes, $"{record.DocumentNumber}-cover.pdf"));
    }

    /// <summary>Pure assembly of the cover sheet — exposed so the renderer test can build a document without repositories.</summary>
    public static RecordPrintDocument BuildDocument(
        ComplianceRecord record,
        RecordTypeDefinition definition,
        TenantComplianceSettings? tenantSettings,
        IReadOnlyList<Domain.RegulatoryBasis.RegulatoryBasis> basis,
        DateTimeOffset generatedAtUtc)
    {
        var frame = new RecordPrintFrame(
            tenantSettings?.CarrierLegalName ?? TenantComplianceSettings.DefaultCarrierLegalName,
            tenantSettings?.NscNumber ?? "NSC number: —",
            tenantSettings?.PrincipalPlaceOfBusiness ?? TenantComplianceSettings.DefaultPrincipalPlaceOfBusiness,
            record.Id,
            record.DocumentNumber,
            record.DocumentVersion,
            generatedAtUtc);

        var fields = new List<PrintField>
        {
            new("Record type", $"{definition.Code} — {definition.DisplayName}"),
            new("Forms", definition.FormCodes.Count > 0 ? string.Join(", ", definition.FormCodes) : "—"),
            new("Title", record.Title),
            new("Subject", $"{record.Subject.Kind}: {record.Subject.Display}"),
            new("Source", $"{record.SourceModule} / {record.SourceRecordId}"),
            new("Keeper", definition.Keeper.ToString()),
            new("Status", record.Status.ToString()),
            new("Registered", record.RegisteredAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)),
            new("Supporting documents", record.SupportingLinks.Count > 0
                ? string.Join("; ", record.SupportingLinks.Select(l => $"{l.Label} ({l.Kind}: {l.Reference})"))
                : "—"),
        };

        if (record.SupersedesId is { } supersedes)
        {
            fields.Add(new PrintField("Supersedes", supersedes.ToString()));
        }

        var retention = new List<PrintField>
        {
            new("Regulatory minimum", $"{definition.RegulatoryMinimum} ({definition.Citation})"),
            new("Carrier retention", definition.CarrierRetention),
            new("Anchors", record.AnchorDates.Count > 0
                ? string.Join("; ", record.AnchorDates.OrderBy(a => a.Key).Select(a => $"{a.Key} {a.Value:yyyy-MM-dd}"))
                : "—"),
            new("Retain until", definition.Rule.IsPermanent
                ? "Permanent — never disposed"
                : record.RetentionPending
                    ? "Pending — an anchor date is not yet known"
                    : record.RetainUntil?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—"),
            new("Holds", record.Holds.Count > 0 ? string.Join("; ", record.Holds.Select(h => h.Reason)) : "None"),
        };

        var byKey = basis.ToDictionary(b => b.Key, StringComparer.Ordinal);
        var basisLines = record.RegulatoryBasisKeys
            .Select(key => byKey.TryGetValue(key, out var b)
                ? new PrintBasisLine(b.Key, b.Citation, b.Title, b.PlainExplanation)
                : new PrintBasisLine(key, "—", "Not in catalogue", string.Empty))
            .ToList();

        return new RecordPrintDocument(frame, record.PrintTemplateKey, $"Record cover sheet — {record.DocumentNumber}", fields, retention, basisLines);
    }
}
