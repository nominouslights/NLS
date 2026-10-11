using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Records.Queries;

public sealed record GetRecordQuery(Guid TenantId, Guid RecordId) : IQuery<ComplianceRecordResponse>;

public sealed record ListRecordsQuery(Guid TenantId, RecordListFilter Filter) : IQuery<IReadOnlyList<ComplianceRecordResponse>>;

/// <summary>Records to act on: in review already, or reaching the end of retention by <paramref name="DueBefore"/>.</summary>
public sealed record ListRecordsDueForDisposalQuery(Guid TenantId, DateOnly DueBefore) : IQuery<IReadOnlyList<ComplianceRecordResponse>>;

public sealed record GetRetentionStatusQuery(Guid TenantId, RecordSubjectKind SubjectKind, Guid? SubjectId, DateOnly Today) : IQuery<RetentionStatusResponse>;

public sealed record ListDestructionLogQuery(Guid TenantId, DateOnly? From, DateOnly? To) : IQuery<IReadOnlyList<DestructionLogEntryResponse>>;

/// <summary>The s. 11 catalogue — pure, no tenant, no repository.</summary>
public sealed record GetRecordTypeCatalogueQuery : IQuery<IReadOnlyList<RecordTypeResponse>>;
