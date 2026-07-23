using NorthernLink.Clients.Application.ClientContacts;

namespace NorthernLink.Clients.Application.Abstractions;

public interface IClientContactReadService
{
    Task<IReadOnlyList<ClientContactResponse>> GetForClientAsync(Guid clientId, CancellationToken cancellationToken = default);
}
