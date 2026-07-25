using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.Clients;

namespace NorthernLink.Clients.Application.Clients.Create;

public sealed class CreateClientCommandHandler(IClientRepository repository)
    : ICommandHandler<CreateClientCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateClientCommand command, CancellationToken cancellationToken)
    {
        var clientResult = Client.Create(
            command.TenantId,
            command.Name,
            command.Type,
            command.ServiceType,
            command.Tag,
            command.AgreementReference,
            command.Notes);

        if (clientResult.IsFailure)
        {
            return Result.Failure<Guid>(clientResult.Error);
        }

        repository.Add(clientResult.Value);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(clientResult.Value.Id);
    }
}
