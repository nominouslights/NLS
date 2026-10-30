using NorthernLink.Compliance.Application.Records;
using NorthernLink.Compliance.Domain.Records;

namespace NorthernLink.Compliance.Application.Abstractions;

/// <summary>Filters for the record list; every member optional.</summary>
public sealed record RecordListFilter(
    ComplianceRecordType? RecordType,
    RecordSubjectKind? SubjectKind,
    Guid? SubjectId,
    ComplianceRecordStatus? Status,
    DateOnly? DueBefore);

/// <summary>
/// Read side over <c>compliance.rm_compliance_records</c>. No tenant parameter — the DbContext's
/// tenant query filter (and RLS beneath it) scopes every query to the ambient tenant. Like every
/// rm_* read it is a projection poll behind the write side.
/// </summary>
public interface IComplianceRecordReadService
{
    Task<ComplianceRecordResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ComplianceRecordResponse>> ListAsync(RecordListFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Records in review for disposal, plus Active/Superseded ones whose retain-until falls on or before <paramref name="dueBefore"/>.</summary>
    Task<IReadOnlyList<ComplianceRecordResponse>> ListDueForDisposalAsync(DateOnly dueBefore, CancellationToken cancellationToken = default);

    Task<RetentionStatusResponse> GetRetentionStatusAsync(RecordSubjectKind kind, Guid? subjectId, DateOnly today, CancellationToken cancellationToken = default);
}

/// <summary>
/// The retention worker's cross-tenant enumeration: which tenants hold records that may need a
/// sweep tonight. Infrastructure answers it on a system session; the worker then runs each
/// tenant's sweep in its own scope under that tenant.
/// </summary>
public interface IRetentionSweepSource
{
    Task<IReadOnlyList<Guid>> ListTenantsWithRetainedRecordsAsync(CancellationToken cancellationToken = default);
}
