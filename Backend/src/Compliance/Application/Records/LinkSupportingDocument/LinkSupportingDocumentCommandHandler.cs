using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Records.LinkSupportingDocument;

/// <summary>Handles <see cref="LinkSupportingDocumentCommand"/> — NL-SP-02 s. 11; HTA s. 318.8(2) (records accessible in one place).</summary>
public sealed class LinkSupportingDocumentCommandHandler(IComplianceRecordRepository records)
    : ICommandHandler<LinkSupportingDocumentCommand>
{
    public async Task<Result> Handle(LinkSupportingDocumentCommand command, CancellationToken cancellationToken)
    {
        var record = await records.GetByIdAsync(command.RecordId, cancellationToken);
        if (record is null)
        {
            return Result.Failure(ComplianceRecordErrors.NotFound);
        }

        var result = record.LinkSupportingDocument(command.Link, command.ActorId);
        if (result.IsFailure)
        {
            return result;
        }

        if (record.DomainEvents.Count > 0)
        {
            await records.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
