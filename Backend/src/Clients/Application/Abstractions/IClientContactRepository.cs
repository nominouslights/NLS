using NorthernLink.Clients.Domain.ClientContacts;

namespace NorthernLink.Clients.Application.Abstractions;

public interface IClientContactRepository
{
    Task<IReadOnlyList<ClientContact>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default);
    void Add(ClientContact contact);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
