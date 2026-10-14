using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Records.SetAnchorDate;

/// <summary>
/// Handles <see cref="SetAnchorDateCommand"/> — NL-SP-02 s. 11 rows whose period counts from a
/// later event: R10 (NSC 11 Part A s. 1(c), six months after the vehicle leaves control), R19
/// (HTA s. 318.1(4), until the claim closes), R15–R17 (end of employment).
/// </summary>
public sealed class SetAnchorDateCommandHandler(IComplianceRecordRepository records)
    : ICommandHandler<SetAnchorDateCommand>
{
    public async Task<Result> Handle(SetAnchorDateCommand command, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(command.Anchor))
        {
            return Result.Failure(ComplianceRecordErrors.UnknownAnchorKind);
        }

        var record = await records.GetByIdAsync(command.RecordId, cancellationToken);
        if (record is null)
        {
            return Result.Failure(ComplianceRecordErrors.NotFound);
        }

        var rule = ComplianceRecordCatalogue.For(record.RecordType).Rule;
        var result = record.SetAnchorDate(rule, command.Anchor, command.Date, command.ActorId);
        if (result.IsFailure)
        {
            return result;
        }

        await records.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
