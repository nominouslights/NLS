using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Records.Register;

/// <summary>
/// Handles <see cref="RegisterRecordCommand"/> — NL-SP-02 s. 11; HTA s. 318.8(1) (the carrier
/// must keep these records) and s. 318.8(2) (accessible in one place: the registry is that
/// place's index). The number-issuing loop is <see cref="RecordRegistrar"/>.
/// </summary>
public sealed class RegisterRecordCommandHandler(RecordRegistrar registrar)
    : ICommandHandler<RegisterRecordCommand, RegisterRecordResult>
{
    public Task<Result<RegisterRecordResult>> Handle(RegisterRecordCommand command, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(command.RecordType))
        {
            return Task.FromResult(Result.Failure<RegisterRecordResult>(ComplianceRecordErrors.UnknownRecordType));
        }

        var definition = ComplianceRecordCatalogue.For(command.RecordType);
        return registrar.RegisterAsync(command.TenantId, definition, command.Registration, command.ActorId, cancellationToken);
    }
}
