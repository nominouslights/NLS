using NorthernLink.Clients.Domain.ClientContacts;

namespace NorthernLink.Clients.Application.Abstractions;

public interface IClientContactRepository
{
    Task<IReadOnlyList<ClientContact>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default);
    void Add(ClientContact contact);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="action"/> inside a single database transaction (committed on success).
    /// Used by set-primary to flush a demote before a promote against the non-deferrable
    /// one-primary-per-client index without ever committing two primary rows.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
