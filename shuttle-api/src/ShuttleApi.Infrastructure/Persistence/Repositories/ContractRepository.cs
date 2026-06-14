using Microsoft.EntityFrameworkCore;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Infrastructure.Persistence.Repositories;

internal sealed class ContractRepository(AppDbContext dbContext) : IContractRepository
{
    public async Task<IReadOnlyList<Contract>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default) =>
        await dbContext.Contracts
            .AsNoTracking()
            .Include(c => c.RateLines)
            .Where(c => c.ClientId == clientId)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync(cancellationToken);

    public async Task<Contract?> GetActiveByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default) =>
        await dbContext.Contracts
            .Include(c => c.RateLines)
            .FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive, cancellationToken);

    public async Task<Dictionary<Guid, Contract>> GetActiveBatchByClientIdsAsync(
        IEnumerable<Guid> clientIds, CancellationToken cancellationToken = default) =>
        await dbContext.Contracts
            .AsNoTracking()
            .Where(c => clientIds.Contains(c.ClientId) && c.IsActive)
            .ToDictionaryAsync(c => c.ClientId, cancellationToken);

    public async Task<Contract?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Contracts
            .Include(c => c.RateLines)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task AddAsync(Contract contract, CancellationToken cancellationToken = default)
    {
        await dbContext.Contracts.AddAsync(contract, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Contract contract, CancellationToken cancellationToken = default)
    {
        dbContext.Contracts.Update(contract);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRateLineAsync(ContractRateLine rateLine, CancellationToken cancellationToken = default)
    {
        await dbContext.ContractRateLines.AddAsync(rateLine, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ContractRateLine?> GetRateLineByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.ContractRateLines.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task DeleteRateLineAsync(ContractRateLine rateLine, CancellationToken cancellationToken = default)
    {
        dbContext.ContractRateLines.Remove(rateLine);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
