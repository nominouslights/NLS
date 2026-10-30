using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Records.Holds;

/// <summary>Handles <see cref="ReleaseHoldCommand"/> — NL-SP-02 s. 11.</summary>
public sealed class ReleaseHoldCommandHandler(IComplianceRecordRepository records)
    : ICommandHandler<ReleaseHoldCommand>
{
    public async Task<Result> Handle(ReleaseHoldCommand command, CancellationToken cancellationToken)
    {
        var record = await records.GetByIdAsync(command.RecordId, cancellationToken);
        if (record is null)
        {
            return Result.Failure(ComplianceRecordErrors.NotFound);
        }

        var result = record.ReleaseHold(command.HoldId, command.ActorId);
        if (result.IsFailure)
        {
            return result;
        }

        await records.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
