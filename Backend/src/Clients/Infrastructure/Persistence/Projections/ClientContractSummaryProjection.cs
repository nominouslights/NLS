using Microsoft.EntityFrameworkCore;
using NorthernLink.Clients.Domain.Contracts;
using NorthernLink.Clients.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;

namespace NorthernLink.Clients.Infrastructure.Persistence.Projections;

/// <summary>
/// Keeps the active-contract summary columns on <c>clients.rm_clients</c> current when a
/// <b>contract</b> changes (the Fleet retirement-certificate idiom: a second projection
/// riding another aggregate's journal). The incoming aggregate id is a contract id; it
/// resolves the owning client and recomputes that client's summary from all its contracts
/// via the same <see cref="ActiveContractFields"/> the client projection uses.
/// </summary>
internal sealed class ClientContractSummaryProjection : IProjection<ClientsDbContext>
{
    public string AggregateType { get; } = AuditNames.ForAggregate(typeof(Contract));

    public async Task ApplyAsync(ClientsDbContext context, Guid contractId, CancellationToken cancellationToken)
    {
        var contract = await context.Contracts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == contractId, cancellationToken);

        if (contract is null)
        {
            // Contracts are never hard-deleted; nothing to resolve a client from.
            return;
        }

        // The client's read row may still be pending in this very batch (client created and
        // contract added before the worker's first poll), so check the tracker before the table.
        var row = context.ClientReadModels.Local.FirstOrDefault(r => r.Id == contract.ClientId)
            ?? await context.ClientReadModels
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == contract.ClientId, cancellationToken);

        if (row is null)
        {
            // No client row yet — ClientProjection computes the summary when it lands.
            return;
        }

        var contracts = await context.Contracts
            .IgnoreQueryFilters()
            .Where(c => c.ClientId == contract.ClientId)
            .ToListAsync(cancellationToken);

        ActiveContractFields.Apply(row, contracts);
    }

    public async Task RebuildAllAsync(ClientsDbContext context, CancellationToken cancellationToken)
    {
        var rows = await context.ClientReadModels.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var contracts = await context.Contracts.IgnoreQueryFilters().ToListAsync(cancellationToken);

        var contractsByClient = contracts.ToLookup(c => c.ClientId);

        foreach (var row in rows)
        {
            ActiveContractFields.Apply(row, contractsByClient[row.Id].ToList());
        }
    }
}
