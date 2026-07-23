using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Domain.Credentials;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Storage;

namespace NorthernLink.Drivers.Application.Credentials.Remove;

public sealed class RemoveDriverCredentialCommandHandler(
    IDriverCredentialRepository repository,
    IObjectStorage objectStorage) : ICommandHandler<RemoveDriverCredentialCommand>
{
    public async Task<Result> Handle(RemoveDriverCredentialCommand command, CancellationToken cancellationToken)
    {
        var credential = await repository.GetByIdAsync(command.CredentialId, cancellationToken);
        if (credential is null)
        {
            return Result.Failure(CredentialErrors.NotFound);
        }

        repository.Remove(credential);
        await repository.SaveChangesAsync(cancellationToken);

        // Clean up any attached image (best-effort: if storage fails, the DB delete is still committed).
        if (credential.ImageKey is not null)
        {
            try
            {
                await objectStorage.DeleteAsync(credential.ImageKey, cancellationToken);
            }
            catch
            {
                // No-op: the credential is already deleted from the DB. The image becomes orphaned
                // but doesn't block the credential removal itself.
            }
        }

        return Result.Success();
    }
}
