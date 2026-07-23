using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.Clients;
using NorthernLink.Clients.Domain.ClientContacts;

namespace NorthernLink.Clients.Application.ClientContacts.Create;

public sealed class CreateClientContactCommandHandler(
    IClientRepository clientRepository,
    IClientContactRepository contactRepository)
    : ICommandHandler<CreateClientContactCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateClientContactCommand command, CancellationToken cancellationToken)
    {
        // Pre-check: client exists.
        var client = await clientRepository.GetByIdAsync(command.ClientId, cancellationToken);
        if (client is null)
        {
            return Result.Failure<Guid>(ClientErrors.NotFound);
        }

        // Pre-check: if marking as primary, no other primary exists for this client.
        if (command.IsPrimary)
        {
            var existing = await contactRepository.GetByClientIdAsync(command.ClientId, cancellationToken);
            if (existing.Any(c => c.IsPrimary))
            {
                return Result.Failure<Guid>(ClientContactErrors.PrimaryAlreadyExists);
            }
        }

        var contactResult = ClientContact.Create(
            command.TenantId,
            command.ClientId,
            command.Name,
            command.Title,
            command.Email,
            command.Phone,
            command.Notes,
            command.IsPrimary);

        if (contactResult.IsFailure)
        {
            return Result.Failure<Guid>(contactResult.Error);
        }

        contactRepository.Add(contactResult.Value);
        await contactRepository.SaveChangesAsync(cancellationToken);
        return Result.Success(contactResult.Value.Id);
    }
}
