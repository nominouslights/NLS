using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.ClientInteractions;

namespace NorthernLink.Clients.Application.ClientInteractions.Delete;

public sealed class DeleteClientInteractionCommandHandler(IClientInteractionRepository repository)
    : ICommandHandler<DeleteClientInteractionCommand>
{
    public async Task<Result> Handle(DeleteClientInteractionCommand command, CancellationToken cancellationToken)
    {
        var interaction = await repository.GetByIdAsync(command.InteractionId, cancellationToken);
        if (interaction is null)
        {
            return Result.Failure(ClientInteractionErrors.NotFound);
        }

        repository.Remove(interaction);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
