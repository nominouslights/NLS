using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.Clients;

namespace NorthernLink.Clients.Application.Clients.Update;

public sealed class UpdateClientCommandHandler(IClientRepository repository)
    : ICommandHandler<UpdateClientCommand>
{
    public async Task<Result> Handle(UpdateClientCommand command, CancellationToken cancellationToken)
    {
        var client = await repository.GetByIdAsync(command.ClientId, cancellationToken);
        if (client is null)
        {
            return Result.Failure(ClientErrors.NotFound);
        }

        var result = client.Update(command.Name, command.ServiceType, command.Tag, command.AgreementReference, command.Notes);
        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
