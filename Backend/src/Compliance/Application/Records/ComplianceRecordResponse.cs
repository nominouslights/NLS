using NorthernLink.Compliance.Domain.Records;

namespace NorthernLink.Compliance.Application.Records;

/// <summary>
/// The wire shape of a compliance record — what <c>GET /api/compliance/records/{id}</c> and the
/// list return. Enums travel as names; <see cref="AnchorDates"/> is keyed by anchor name.
/// </summary>
public sealed record ComplianceRecordResponse(
    Guid Id,
    string RecordType,
    string RecordTypeDisplayName,
    string DocumentNumber,
    string Title,
    string SubjectKind,
    Guid? SubjectId,
    string SubjectDisplay,
    string SourceModule,
    string SourceRecordId,
    IReadOnlyDictionary<string, DateOnly> AnchorDates,
    DateOnly? RetainUntil,
    bool RetentionPending,
    bool IsPermanent,
    string Status,
    string Keeper,
    IReadOnlyList<RecordHoldResponse> Holds,
    IReadOnlyList<SupportingLinkResponse> SupportingLinks,
    string PrintTemplateKey,
    int DocumentVersion,
    Guid? SupersedesId,
    IReadOnlyList<string> RegulatoryBasisKeys,
    Guid? RegisteredBy,
    DateTimeOffset RegisteredAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? DisposedAtUtc);

public sealed record RecordHoldResponse(Guid HoldId, string Reason, Guid? PlacedBy, DateTimeOffset PlacedAtUtc);

public sealed record SupportingLinkResponse(string Kind, string Reference, string Label);

/// <summary>Where a subject (a driver, a vehicle, the carrier) stands on retention, as one summary.</summary>
public sealed record RetentionStatusResponse(
    string SubjectKind,
    Guid? SubjectId,
    int ActiveCount,
    int PendingCount,
    int HeldCount,
    int ReviewForDisposalCount,
    int DisposedCount,
    int SupersededCount,
    DateOnly? NextRetainUntil,
    int OverdueCount);

/// <summary>One destruction-log line on the wire.</summary>
public sealed record DestructionLogEntryResponse(
    Guid Id,
    Guid RecordId,
    string DocumentNumber,
    string RecordType,
    DateTimeOffset DisposedAtUtc,
    Guid? DisposedBy,
    string Method,
    string? Note);

/// <summary>One row of the s. 11 catalogue on the wire — what a registration form offers.</summary>
public sealed record RecordTypeResponse(
    string Code,
    string DisplayName,
    IReadOnlyList<string> FormCodes,
    string SectionElevenRow,
    string Keeper,
    string RegulatoryMinimum,
    string Citation,
    string CarrierRetention,
    bool IsPermanent,
    IReadOnlyList<RetentionTermResponse> RetentionTerms,
    IReadOnlyList<string> AnchorKinds,
    string Series,
    string FormNumber,
    string PrintTemplateKey,
    IReadOnlyList<string> RegulatoryBasisKeys);

public sealed record RetentionTermResponse(string Anchor, int OffsetMonths);

/// <summary>Result of a registration: the id and number, and whether this call created it (false on an idempotent replay).</summary>
public sealed record RegisterRecordResult(Guid RecordId, string DocumentNumber, bool Created);

/// <summary>A batch disposal's account: every id either disposed or refused with the error that says why.</summary>
public sealed record DisposalResult(IReadOnlyList<Guid> Disposed, IReadOnlyList<DisposalRefusal> Refused);

public sealed record DisposalRefusal(Guid RecordId, string Code, string Message, IReadOnlyList<string> Basis);

public static class RecordTypeResponseMapper
{
    public static RecordTypeResponse ToResponse(RecordTypeDefinition d) => new(
        d.Code,
        d.DisplayName,
        d.FormCodes,
        d.SectionElevenRow,
        d.Keeper.ToString(),
        d.RegulatoryMinimum,
        d.Citation,
        d.CarrierRetention,
        d.Rule.IsPermanent,
        d.Rule.Terms.Select(t => new RetentionTermResponse(t.Anchor.ToString(), t.OffsetMonths)).ToList(),
        d.AnchorKinds.Select(a => a.ToString()).ToList(),
        d.Series.ToString(),
        d.FormNumber,
        d.PrintTemplateKey,
        d.RegulatoryBasisKeys);
}
