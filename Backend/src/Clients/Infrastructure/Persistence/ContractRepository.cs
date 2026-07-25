using Microsoft.EntityFrameworkCore;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.Contracts;

namespace NorthernLink.Clients.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="ClientsDbContext"/> (tenant-filtered).</summary>
internal sealed class ContractRepository(ClientsDbContext context) : IContractRepository
{
    public Task<Contract?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Contracts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Contract>> GetByClientIdAsync(
        Guid clientId,
        CancellationToken cancellationToken = default) =>
        await context.Contracts
            .Where(c => c.ClientId == clientId)
            .ToListAsync(cancellationToken);

    public void Add(Contract contract) => context.Contracts.Add(contract);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
