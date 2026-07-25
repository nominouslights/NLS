using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Domain.Credentials;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Credentials.Add;

public sealed class AddDriverCredentialCommandHandler(IDriverCredentialRepository repository)
    : ICommandHandler<AddDriverCredentialCommand, Guid>
{
    public async Task<Result<Guid>> Handle(AddDriverCredentialCommand command, CancellationToken cancellationToken)
    {
        if (!await repository.DriverExistsAsync(command.DriverId, cancellationToken))
        {
            return Result.Failure<Guid>(DriverErrors.NotFound);
        }

        var credentialResult = DriverCredential.Add(
            command.TenantId,
            command.DriverId,
            command.Type,
            command.Label,
            command.Issued,
            command.Expiry,
            command.Optional,
            command.Note);

        if (credentialResult.IsFailure)
        {
            return Result.Failure<Guid>(credentialResult.Error);
        }

        repository.Add(credentialResult.Value);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(credentialResult.Value.Id);
    }
}
