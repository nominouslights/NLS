using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Records.Holds;

/// <summary>
/// Handles <see cref="PlaceHoldCommand"/> — NL-SP-02 s. 11; HTA s. 318.10(2) (an inspector may
/// require production of any record): a record under investigation, audit or claim is held and
/// never reaches disposal review while the hold stands.
/// </summary>
public sealed class PlaceHoldCommandHandler(IComplianceRecordRepository records)
    : ICommandHandler<PlaceHoldCommand, Guid>
{
    public async Task<Result<Guid>> Handle(PlaceHoldCommand command, CancellationToken cancellationToken)
    {
        var record = await records.GetByIdAsync(command.RecordId, cancellationToken);
        if (record is null)
        {
            return Result.Failure<Guid>(ComplianceRecordErrors.NotFound);
        }

        var result = record.PlaceHold(command.Reason, command.ActorId, DateTimeOffset.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        await records.SaveChangesAsync(cancellationToken);
        return result;
    }
}
