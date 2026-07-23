using NorthernLink.Billing.Domain.Contracts;

namespace NorthernLink.Billing.Application.Abstractions;

/// <summary>
/// The <c>contract_snapshots</c> replica. The upsert lookup takes an explicit tenant id
/// because its caller is the integration-event consumer, which runs outside any HTTP
/// request (no ambient tenant — the Fleet inspection-consumer pattern).
/// </summary>
public interface IContractSnapshotRepository
{
    Task<ContractSnapshot?> GetByIdAsync(Guid tenantId, Guid contractId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContractSnapshot>> GetForClientAsync(Guid clientId, CancellationToken cancellationToken = default);

    void Add(ContractSnapshot snapshot);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
