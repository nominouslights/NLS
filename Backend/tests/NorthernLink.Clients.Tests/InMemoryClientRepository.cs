using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.Clients;

namespace NorthernLink.Clients.Tests;

/// <summary>In-memory fake of the write-side repository for handler tests.</summary>
internal sealed class InMemoryClientRepository : IClientRepository
{
    public List<Client> Clients { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Clients.FirstOrDefault(c => c.Id == id));

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Clients.Any(c => c.Id == id));

    public void Add(Client client) => Clients.Add(client);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
