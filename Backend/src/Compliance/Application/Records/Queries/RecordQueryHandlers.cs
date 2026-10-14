using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Records.Queries;

/// <summary>Handles <see cref="GetRecordQuery"/> — NL-SP-02 s. 11; HTA s. 318.8(2).</summary>
public sealed class GetRecordQueryHandler(IComplianceRecordReadService readService)
    : IQueryHandler<GetRecordQuery, ComplianceRecordResponse>
{
    public async Task<Result<ComplianceRecordResponse>> Handle(GetRecordQuery query, CancellationToken cancellationToken)
    {
        var record = await readService.GetAsync(query.RecordId, cancellationToken);
        return record is null
            ? Result.Failure<ComplianceRecordResponse>(ComplianceRecordErrors.NotFound)
            : Result.Success(record);
    }
}

/// <summary>Handles <see cref="ListRecordsQuery"/> — NL-SP-02 s. 11. An empty registry is an empty list.</summary>
public sealed class ListRecordsQueryHandler(IComplianceRecordReadService readService)
    : IQueryHandler<ListRecordsQuery, IReadOnlyList<ComplianceRecordResponse>>
{
    public async Task<Result<IReadOnlyList<ComplianceRecordResponse>>> Handle(ListRecordsQuery query, CancellationToken cancellationToken) =>
        Result.Success(await readService.ListAsync(query.Filter, cancellationToken));
}

/// <summary>Handles <see cref="ListRecordsDueForDisposalQuery"/> — NL-SP-02 s. 11 (disposal review).</summary>
public sealed class ListRecordsDueForDisposalQueryHandler(IComplianceRecordReadService readService)
    : IQueryHandler<ListRecordsDueForDisposalQuery, IReadOnlyList<ComplianceRecordResponse>>
{
    public async Task<Result<IReadOnlyList<ComplianceRecordResponse>>> Handle(ListRecordsDueForDisposalQuery query, CancellationToken cancellationToken) =>
        Result.Success(await readService.ListDueForDisposalAsync(query.DueBefore, cancellationToken));
}

/// <summary>Handles <see cref="GetRetentionStatusQuery"/> — NL-SP-02 s. 11; HTA s. 318.8(1).</summary>
public sealed class GetRetentionStatusQueryHandler(IComplianceRecordReadService readService)
    : IQueryHandler<GetRetentionStatusQuery, RetentionStatusResponse>
{
    public async Task<Result<RetentionStatusResponse>> Handle(GetRetentionStatusQuery query, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(query.SubjectKind))
        {
            return Result.Failure<RetentionStatusResponse>(ComplianceRecordErrors.UnknownSubjectKind);
        }

        return Result.Success(await readService.GetRetentionStatusAsync(query.SubjectKind, query.SubjectId, query.Today, cancellationToken));
    }
}

/// <summary>Handles <see cref="ListDestructionLogQuery"/> — NL-SP-02 s. 11; HTA s. 318.10(2).</summary>
public sealed class ListDestructionLogQueryHandler(IDestructionLogRepository destructionLog)
    : IQueryHandler<ListDestructionLogQuery, IReadOnlyList<DestructionLogEntryResponse>>
{
    public async Task<Result<IReadOnlyList<DestructionLogEntryResponse>>> Handle(ListDestructionLogQuery query, CancellationToken cancellationToken)
    {
        var entries = await destructionLog.ListAsync(query.From, query.To, cancellationToken);
        IReadOnlyList<DestructionLogEntryResponse> response = entries
            .Select(e => new DestructionLogEntryResponse(
                e.Id, e.RecordId, e.DocumentNumber, e.RecordType.ToString(), e.DisposedAtUtc, e.DisposedBy, e.Method, e.Note))
            .ToList();
        return Result.Success(response);
    }
}

/// <summary>Handles <see cref="GetRecordTypeCatalogueQuery"/> — the s. 11 table of NL-SP-02, as code.</summary>
public sealed class GetRecordTypeCatalogueQueryHandler
    : IQueryHandler<GetRecordTypeCatalogueQuery, IReadOnlyList<RecordTypeResponse>>
{
    public Task<Result<IReadOnlyList<RecordTypeResponse>>> Handle(GetRecordTypeCatalogueQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<RecordTypeResponse> response = ComplianceRecordCatalogue.All.Select(RecordTypeResponseMapper.ToResponse).ToList();
        return Task.FromResult(Result.Success(response));
    }
}
