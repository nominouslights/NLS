using NorthernLink.Clients.Domain.Contracts;

namespace NorthernLink.Clients.Application.Abstractions;

/// <summary>Write-side persistence for the Contract aggregate (tenant-scoped).</summary>
public interface IContractRepository
{
    Task<Contract?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Every contract of a client, for the overlap check on create/update.</summary>
    Task<IReadOnlyList<Contract>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default);

    void Add(Contract contract);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
