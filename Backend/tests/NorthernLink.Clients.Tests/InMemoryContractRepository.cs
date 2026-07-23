using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.Contracts;

namespace NorthernLink.Clients.Tests;

/// <summary>In-memory fake of the write-side repository for handler tests.</summary>
internal sealed class InMemoryContractRepository : IContractRepository
{
    public List<Contract> Contracts { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public Task<Contract?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Contracts.FirstOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<Contract>> GetByClientIdAsync(
        Guid clientId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Contract>>(Contracts.Where(c => c.ClientId == clientId).ToList());

    public void Add(Contract contract) => Contracts.Add(contract);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
