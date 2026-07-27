using NorthernLink.Clients.Domain.ClientInteractions;

namespace NorthernLink.Clients.Application.Abstractions;

/// <summary>Write-side persistence for the ClientInteraction aggregate (tenant-scoped).</summary>
public interface IClientInteractionRepository
{
    Task<ClientInteraction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(ClientInteraction interaction);

    void Remove(ClientInteraction interaction);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
