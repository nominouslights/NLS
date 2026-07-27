using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.ClientContacts;

namespace NorthernLink.Clients.Application.ClientContacts.SetPrimary;

public sealed class SetPrimaryClientContactCommandHandler(IClientContactRepository repository)
    : ICommandHandler<SetPrimaryClientContactCommand>
{
    public async Task<Result> Handle(SetPrimaryClientContactCommand command, CancellationToken cancellationToken)
    {
        var contacts = await repository.GetByClientIdAsync(command.ClientId, cancellationToken);

        var target = contacts.FirstOrDefault(c => c.Id == command.ContactId);
        if (target is null)
        {
            return Result.Failure(ClientContactErrors.NotFound);
        }

        var current = contacts.FirstOrDefault(c => c.IsPrimary);
        if (current is not null && current.Id == target.Id)
        {
            // Already primary — nothing to change, no write, no event.
            return Result.Success();
        }

        // The partial unique index (one is_primary per client) is NOT deferrable, so a single
        // batched SaveChanges could emit the promote UPDATE before the demote UPDATE and briefly
        // leave two primary rows — which the index rejects. Order the writes inside one
        // transaction and flush the demote before the promote: after the demote flush there are
        // zero primaries committed, after the promote flush exactly one, so no committed moment
        // ever has two.
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            if (current is not null)
            {
                current.ClearPrimary();
                await repository.SaveChangesAsync(ct);
            }

            target.SetPrimary();
            await repository.SaveChangesAsync(ct);
        }, cancellationToken);

        return Result.Success();
    }
}
