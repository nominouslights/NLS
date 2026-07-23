using Microsoft.EntityFrameworkCore;
using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Domain.Contracts;

namespace NorthernLink.Billing.Infrastructure.Persistence;

/// <summary>
/// The <c>contract_snapshots</c> replica over <see cref="BillingDbContext"/>. The upsert
/// lookup is called by the integration-event consumer, which runs outside any HTTP
/// request: the ambient tenant filter would compare against null and match nothing, so it
/// bypasses the filter and matches the tenant id carried by the event instead (the Fleet
/// inspection-consumer pattern). <see cref="GetForClientAsync"/> runs on the request path
/// and stays behind the tenant filter.
/// </summary>
internal sealed class ContractSnapshotRepository(BillingDbContext context) : IContractSnapshotRepository
{
    public Task<ContractSnapshot?> GetByIdAsync(
        Guid tenantId,
        Guid contractId,
        CancellationToken cancellationToken = default) =>
        context.ContractSnapshots
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == contractId, cancellationToken);

    public async Task<IReadOnlyList<ContractSnapshot>> GetForClientAsync(
        Guid clientId,
        CancellationToken cancellationToken = default) =>
        await context.ContractSnapshots
            .Where(c => c.ClientId == clientId)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync(cancellationToken);

    public void Add(ContractSnapshot snapshot) => context.ContractSnapshots.Add(snapshot);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
