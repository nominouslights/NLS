using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.ClientContacts;

namespace NorthernLink.Clients.Tests;

/// <summary>In-memory fake of the write-side contact repository for handler tests.</summary>
internal sealed class InMemoryClientContactRepository : IClientContactRepository
{
    public List<ClientContact> Contacts { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public Task<IReadOnlyList<ClientContact>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ClientContact>>(Contacts.Where(c => c.ClientId == clientId).ToList());

    public void Add(ClientContact contact) => Contacts.Add(contact);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }

    // The fake has no real transaction — just run the action. The demote/promote ordering the
    // handler performs inside is what the unit tests assert.
    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default) =>
        action(cancellationToken);
}
