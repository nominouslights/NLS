using Microsoft.EntityFrameworkCore;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.ClientContacts;

namespace NorthernLink.Clients.Infrastructure.Persistence;

internal sealed class ClientContactRepository(ClientsDbContext context) : IClientContactRepository
{
    public async Task<ClientContact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.ClientContacts
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ClientContact>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        return await context.ClientContacts
            .Where(c => c.ClientId == clientId)
            .ToListAsync(cancellationToken);
    }

    public void Add(ClientContact contact)
    {
        context.ClientContacts.Add(contact);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
