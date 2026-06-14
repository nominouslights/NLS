namespace ShuttleApi.Domain.Clients;

public interface IContractRepository
{
    Task<IReadOnlyList<Contract>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<Contract?> GetActiveByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, Contract>> GetActiveBatchByClientIdsAsync(IEnumerable<Guid> clientIds, CancellationToken cancellationToken = default);
    Task<Contract?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Contract contract, CancellationToken cancellationToken = default);
    Task UpdateAsync(Contract contract, CancellationToken cancellationToken = default);
    Task AddRateLineAsync(ContractRateLine rateLine, CancellationToken cancellationToken = default);
    Task<ContractRateLine?> GetRateLineByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteRateLineAsync(ContractRateLine rateLine, CancellationToken cancellationToken = default);
}
