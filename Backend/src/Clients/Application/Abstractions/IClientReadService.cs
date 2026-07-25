using NorthernLink.Clients.Application.Clients;

namespace NorthernLink.Clients.Application.Abstractions;

/// <summary>Read side for client queries — returns response DTOs directly (tenant-scoped).</summary>
public interface IClientReadService
{
    Task<IReadOnlyList<ClientResponse>> GetClientsAsync(CancellationToken cancellationToken = default);

    Task<ClientResponse?> GetClientAsync(Guid clientId, CancellationToken cancellationToken = default);
}
