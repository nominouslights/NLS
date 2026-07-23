using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Domain.Credentials;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Credentials.SetImage;

public sealed class SetDriverCredentialImageCommandHandler(
    IDriverCredentialRepository repository) : ICommandHandler<SetDriverCredentialImageCommand>
{
    public async Task<Result> Handle(SetDriverCredentialImageCommand command, CancellationToken cancellationToken)
    {
        var credential = await repository.GetByIdAsync(command.CredentialId, cancellationToken);
        if (credential is null || credential.DriverId != command.DriverId)
        {
            return Result.Failure(CredentialErrors.NotFound);
        }

        credential.SetImage(command.ImageKey, command.ImageContentType);
        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
