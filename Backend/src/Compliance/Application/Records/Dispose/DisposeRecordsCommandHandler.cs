using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Domain;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Records.Dispose;

/// <summary>
/// Handles <see cref="DisposeRecordsCommand"/> — NL-SP-02 s. 11 (disposal only after the
/// retention period, reviewed by a person, logged); HTA s. 318.10(2) (a held record stays
/// producible). Permanent types (R21, NSC 15) are refused outright.
/// </summary>
public sealed class DisposeRecordsCommandHandler(
    IComplianceRecordRepository records,
    IDestructionLogRepository destructionLog)
    : ICommandHandler<DisposeRecordsCommand, DisposalResult>
{
    public async Task<Result<DisposalResult>> Handle(DisposeRecordsCommand command, CancellationToken cancellationToken)
    {
        if (command.RecordIds.Count == 0)
        {
            return Result.Failure<DisposalResult>(ComplianceRecordErrors.DisposalIdsRequired);
        }

        if (string.IsNullOrWhiteSpace(command.Method))
        {
            return Result.Failure<DisposalResult>(ComplianceRecordErrors.DisposalMethodRequired);
        }

        var ids = command.RecordIds.Distinct().ToList();
        var found = (await records.GetByIdsAsync(ids, cancellationToken)).ToDictionary(r => r.Id);
        var now = DateTimeOffset.UtcNow;

        var disposed = new List<Guid>();
        var refused = new List<DisposalRefusal>();

        foreach (var id in ids)
        {
            if (!found.TryGetValue(id, out var record))
            {
                refused.Add(Refusal(id, ComplianceRecordErrors.NotFound));
                continue;
            }

            var rule = ComplianceRecordCatalogue.For(record.RecordType).Rule;
            var result = record.Dispose(rule, command.Method, command.Note, command.ActorId, now);
            if (result.IsFailure)
            {
                refused.Add(Refusal(id, result.Error));
                continue;
            }

            destructionLog.Add(DestructionLogEntry.For(record, now, command.ActorId, command.Method.Trim(), command.Note?.Trim()));
            disposed.Add(id);
        }

        if (disposed.Count > 0)
        {
            await records.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new DisposalResult(disposed, refused));
    }

    private static DisposalRefusal Refusal(Guid id, Error error) =>
        new(id, error.Code, error.Message, ComplianceError.BasisKeysFor(error.Code));
}
