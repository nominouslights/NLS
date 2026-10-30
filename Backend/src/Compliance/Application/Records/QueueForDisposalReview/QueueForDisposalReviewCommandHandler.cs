using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Records.QueueForDisposalReview;

/// <summary>
/// Handles <see cref="QueueForDisposalReviewCommand"/> — NL-SP-02 s. 11: records are kept for
/// their stated period and disposed only after review. Recomputing first means a rule-set change
/// or a late anchor is honoured before anything is queued.
/// </summary>
public sealed class QueueForDisposalReviewCommandHandler(IComplianceRecordRepository records)
    : ICommandHandler<QueueForDisposalReviewCommand, int>
{
    public async Task<Result<int>> Handle(QueueForDisposalReviewCommand command, CancellationToken cancellationToken)
    {
        var retained = await records.ListRetainedAsync(cancellationToken);
        var changed = false;
        var queued = 0;

        foreach (var record in retained)
        {
            var rule = ComplianceRecordCatalogue.For(record.RecordType).Rule;
            changed |= record.RecomputeRetention(rule);
            if (record.QueueForDisposalReview(command.AsOf))
            {
                queued++;
                changed = true;
            }
        }

        if (changed)
        {
            await records.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(queued);
    }
}
