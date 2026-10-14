using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Records.Supersede;

/// <summary>
/// Handles <see cref="SupersedeRecordCommand"/> — NL-SP-02 s. 11 rows R6, R11 and R20 ("current
/// version … superseded kept N months"): a new version is a new numbered record, and the old one's
/// retention clock starts at supersession.
/// </summary>
public sealed class SupersedeRecordCommandHandler(RecordRegistrar registrar)
    : ICommandHandler<SupersedeRecordCommand, RegisterRecordResult>
{
    public Task<Result<RegisterRecordResult>> Handle(SupersedeRecordCommand command, CancellationToken cancellationToken) =>
        registrar.SupersedeAsync(command.TenantId, command.RecordId, command.Registration, command.ActorId, cancellationToken);
}
