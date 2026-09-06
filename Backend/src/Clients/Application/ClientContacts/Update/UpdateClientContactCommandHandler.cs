using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.ClientContacts;

namespace NorthernLink.Clients.Application.ClientContacts.Update;

public sealed class UpdateClientContactCommandHandler(IClientContactRepository contactRepository)
    : ICommandHandler<UpdateClientContactCommand>
{
    public async Task<Result> Handle(UpdateClientContactCommand command, CancellationToken cancellationToken)
    {
        // The contact must exist AND belong to the client in the route — a contact id under
        // the wrong client is indistinguishable from a missing one to the caller.
        var contact = await contactRepository.GetByIdAsync(command.ContactId, cancellationToken);
        if (contact is null || contact.ClientId != command.ClientId)
        {
            return Result.Failure(ClientContactErrors.NotFound);
        }

        // Pre-check: promoting to primary requires no OTHER primary contact (mirrors the
        // create handler's rule; re-saving an already-primary contact stays valid).
        if (command.IsPrimary && !contact.IsPrimary)
        {
            var existing = await contactRepository.GetByClientIdAsync(command.ClientId, cancellationToken);
            if (existing.Any(c => c.IsPrimary && c.Id != contact.Id))
            {
                return Result.Failure(ClientContactErrors.PrimaryAlreadyExists);
            }
        }

        var result = contact.Update(
            command.Name,
            command.Title,
            command.Email,
            command.Phone,
            command.Notes,
            command.IsPrimary,
            command.ReceivesEmailReports,
            command.ReceivesAccrualsReports);

        if (result.IsFailure)
        {
            return result;
        }

        await contactRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
