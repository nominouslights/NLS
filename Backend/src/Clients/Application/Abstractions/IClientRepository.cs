using NorthernLink.Clients.Domain.Clients;

namespace NorthernLink.Clients.Application.Abstractions;

/// <summary>Write-side persistence for the Client aggregate (tenant-scoped).</summary>
public interface IClientRepository
{
    Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(Client client);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
