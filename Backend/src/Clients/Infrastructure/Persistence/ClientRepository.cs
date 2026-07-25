using Microsoft.EntityFrameworkCore;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.Clients;

namespace NorthernLink.Clients.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="ClientsDbContext"/> (tenant-filtered).</summary>
internal sealed class ClientRepository(ClientsDbContext context) : IClientRepository
{
    public Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Clients.AnyAsync(c => c.Id == id, cancellationToken);

    public void Add(Client client) => context.Clients.Add(client);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
