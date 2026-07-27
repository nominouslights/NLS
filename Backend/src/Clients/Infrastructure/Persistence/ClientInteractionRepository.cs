using Microsoft.EntityFrameworkCore;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.ClientInteractions;

namespace NorthernLink.Clients.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="ClientsDbContext"/> (tenant-filtered).</summary>
internal sealed class ClientInteractionRepository(ClientsDbContext context) : IClientInteractionRepository
{
    public Task<ClientInteraction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.ClientInteractions.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public void Add(ClientInteraction interaction) => context.ClientInteractions.Add(interaction);

    public void Remove(ClientInteraction interaction) => context.ClientInteractions.Remove(interaction);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
