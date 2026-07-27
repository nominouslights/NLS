using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.Clients;
using NorthernLink.Clients.Domain.ClientInteractions;

namespace NorthernLink.Clients.Application.ClientInteractions.Create;

public sealed class CreateClientInteractionCommandHandler(
    IClientRepository clientRepository,
    IClientInteractionRepository interactionRepository)
    : ICommandHandler<CreateClientInteractionCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateClientInteractionCommand command, CancellationToken cancellationToken)
    {
        if (!await clientRepository.ExistsAsync(command.ClientId, cancellationToken))
        {
            return Result.Failure<Guid>(ClientErrors.NotFound);
        }

        var interactionResult = ClientInteraction.Create(
            command.TenantId,
            command.ClientId,
            command.Type,
            command.OccurredOn,
            command.Summary,
            command.ParticipantContactIds,
            command.FollowUpDate,
            command.FollowUpNote);

        if (interactionResult.IsFailure)
        {
            return Result.Failure<Guid>(interactionResult.Error);
        }

        interactionRepository.Add(interactionResult.Value);
        await interactionRepository.SaveChangesAsync(cancellationToken);
        return Result.Success(interactionResult.Value.Id);
    }
}
