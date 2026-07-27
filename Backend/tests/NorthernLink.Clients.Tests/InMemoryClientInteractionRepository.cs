using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.ClientInteractions;

namespace NorthernLink.Clients.Tests;

/// <summary>In-memory fake of the write-side interaction repository for handler tests.</summary>
internal sealed class InMemoryClientInteractionRepository : IClientInteractionRepository
{
    public List<ClientInteraction> Interactions { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public Task<ClientInteraction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Interactions.FirstOrDefault(i => i.Id == id));

    public void Add(ClientInteraction interaction) => Interactions.Add(interaction);

    public void Remove(ClientInteraction interaction) => Interactions.Remove(interaction);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
