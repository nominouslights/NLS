using Microsoft.EntityFrameworkCore;
using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Application.Records;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Compliance.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Compliance.Infrastructure.Persistence;

/// <summary>
/// Read side — queries compliance.rm_compliance_records (tenant query filter + RLS) and maps to
/// the public contract. Like every rm_* read it is a projection poll behind the write side, so a
/// record registered a moment ago can briefly be absent from the list or 404 by id (the print
/// path reads the aggregate for that reason).
/// </summary>
internal sealed class ComplianceRecordReadService(ComplianceDbContext context) : IComplianceRecordReadService
{
    public async Task<ComplianceRecordResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await context.ComplianceRecordReadModels.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        return row is null ? null : ToResponse(row);
    }

    public async Task<IReadOnlyList<ComplianceRecordResponse>> ListAsync(RecordListFilter filter, CancellationToken cancellationToken = default)
    {
        var query = context.ComplianceRecordReadModels.AsNoTracking();

        if (filter.RecordType is { } type)
        {
            var typeName = type.ToString();
            query = query.Where(r => r.RecordType == typeName);
        }

        if (filter.SubjectKind is { } kind)
        {
            var kindName = kind.ToString();
            query = query.Where(r => r.SubjectKind == kindName);
        }

        if (filter.SubjectId is { } subjectId)
        {
            query = query.Where(r => r.SubjectId == subjectId);
        }

        if (filter.Status is { } status)
        {
            var statusName = status.ToString();
            query = query.Where(r => r.Status == statusName);
        }

        if (filter.DueBefore is { } dueBefore)
        {
            query = query.Where(r => r.RetainUntil != null && r.RetainUntil <= dueBefore);
        }

        var rows = await query
            .OrderByDescending(r => r.RegisteredAtUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(ToResponse).ToList();
    }

    public async Task<IReadOnlyList<ComplianceRecordResponse>> ListDueForDisposalAsync(DateOnly dueBefore, CancellationToken cancellationToken = default)
    {
        var review = nameof(ComplianceRecordStatus.ReviewForDisposal);
        var active = nameof(ComplianceRecordStatus.Active);
        var superseded = nameof(ComplianceRecordStatus.Superseded);

        var rows = await context.ComplianceRecordReadModels.AsNoTracking()
            .Where(r => r.Status == review
                || ((r.Status == active || r.Status == superseded)
                    && !r.RetentionPending && !r.IsPermanent && !r.IsHeld
                    && r.RetainUntil != null && r.RetainUntil <= dueBefore))
            .OrderBy(r => r.RetainUntil)
            .ToListAsync(cancellationToken);

        return rows.Select(ToResponse).ToList();
    }

    public async Task<RetentionStatusResponse> GetRetentionStatusAsync(RecordSubjectKind kind, Guid? subjectId, DateOnly today, CancellationToken cancellationToken = default)
    {
        var kindName = kind.ToString();
        var query = context.ComplianceRecordReadModels.AsNoTracking().Where(r => r.SubjectKind == kindName);
        if (subjectId is { } id)
        {
            query = query.Where(r => r.SubjectId == id);
        }

        var rows = await query
            .Select(r => new { r.Status, r.RetentionPending, r.IsHeld, r.RetainUntil, r.IsPermanent })
            .ToListAsync(cancellationToken);

        var active = nameof(ComplianceRecordStatus.Active);
        var retained = rows.Where(r => r.Status == active).ToList();

        return new RetentionStatusResponse(
            kindName,
            subjectId,
            ActiveCount: retained.Count,
            PendingCount: retained.Count(r => r.RetentionPending),
            HeldCount: rows.Count(r => r.IsHeld && r.Status != nameof(ComplianceRecordStatus.Disposed)),
            ReviewForDisposalCount: rows.Count(r => r.Status == nameof(ComplianceRecordStatus.ReviewForDisposal)),
            DisposedCount: rows.Count(r => r.Status == nameof(ComplianceRecordStatus.Disposed)),
            SupersededCount: rows.Count(r => r.Status == nameof(ComplianceRecordStatus.Superseded)),
            NextRetainUntil: retained.Where(r => r.RetainUntil is not null && !r.IsPermanent).Min(r => r.RetainUntil),
            OverdueCount: retained.Count(r => !r.IsHeld && !r.RetentionPending && r.RetainUntil is { } until && until <= today));
    }

    private static ComplianceRecordResponse ToResponse(ComplianceRecordReadModel row)
    {
        var definition = ComplianceRecordCatalogue.TryParse(row.RecordType, out var type) ? ComplianceRecordCatalogue.For(type) : null;
        return new ComplianceRecordResponse(
            row.Id,
            row.RecordType,
            definition?.DisplayName ?? row.RecordType,
            row.DocumentNumber,
            row.Title,
            row.SubjectKind,
            row.SubjectId,
            row.SubjectDisplay,
            row.SourceModule,
            row.SourceRecordId,
            row.AnchorDates,
            row.RetainUntil,
            row.RetentionPending,
            row.IsPermanent,
            row.Status,
            definition?.Keeper.ToString() ?? string.Empty,
            row.Holds.Select(h => new RecordHoldResponse(h.HoldId, h.Reason, h.PlacedBy, h.PlacedAtUtc)).ToList(),
            row.SupportingLinks.Select(l => new SupportingLinkResponse(l.Kind, l.Reference, l.Label)).ToList(),
            row.PrintTemplateKey,
            row.DocumentVersion,
            row.SupersedesId,
            row.RegulatoryBasisKeys,
            row.RegisteredBy,
            row.RegisteredAtUtc,
            row.UpdatedAtUtc,
            row.DisposedAtUtc);
    }
}
