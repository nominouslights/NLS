using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Domain.Contracts;

namespace NorthernLink.Billing.Tests;

/// <summary>In-memory fake for consumer/handler tests — no EF, no Postgres.</summary>
public sealed class InMemoryContractSnapshotRepository : IContractSnapshotRepository
{
    public List<ContractSnapshot> Snapshots { get; } = [];

    public int SaveCount { get; private set; }

    public Task<ContractSnapshot?> GetByIdAsync(
        Guid tenantId, Guid contractId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Snapshots.FirstOrDefault(c => c.TenantId == tenantId && c.Id == contractId));

    public Task<IReadOnlyList<ContractSnapshot>> GetForClientAsync(
        Guid clientId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ContractSnapshot>>(
            Snapshots.Where(c => c.ClientId == clientId).ToList());

    public void Add(ContractSnapshot snapshot) => Snapshots.Add(snapshot);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
