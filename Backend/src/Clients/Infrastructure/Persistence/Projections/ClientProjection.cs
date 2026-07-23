using Microsoft.EntityFrameworkCore;
using NorthernLink.Clients.Domain.Clients;
using NorthernLink.Clients.Domain.Contracts;
using NorthernLink.Clients.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;

namespace NorthernLink.Clients.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="Client"/> into <c>clients.rm_clients</c>. Can't use the shared
/// <see cref="ClientsProjection{TSource,TRead}"/> base: the row denormalizes an
/// active-contract summary, so mapping a client also reads that client's write-side
/// contracts (same module, same schema — not a cross-module join).
/// </summary>
internal sealed class ClientProjection : IProjection<ClientsDbContext>
{
    public string AggregateType { get; } = AuditNames.ForAggregate(typeof(Client));

    public async Task ApplyAsync(ClientsDbContext context, Guid clientId, CancellationToken cancellationToken)
    {
        var client = await context.Clients
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);

        var row = await context.ClientReadModels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == clientId, cancellationToken);

        // Source gone (hard delete) — the read row goes with it.
        if (client is null)
        {
            if (row is not null)
            {
                context.ClientReadModels.Remove(row);
            }

            return;
        }

        var contracts = await context.Contracts
            .IgnoreQueryFilters()
            .Where(c => c.ClientId == clientId)
            .ToListAsync(cancellationToken);

        if (row is null)
        {
            row = new ClientReadModel();
            Map(client, contracts, row);
            context.ClientReadModels.Add(row);
        }
        else
        {
            Map(client, contracts, row);
        }
    }

    public async Task RebuildAllAsync(ClientsDbContext context, CancellationToken cancellationToken)
    {
        var clients = await context.Clients.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var contracts = await context.Contracts.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var rows = await context.ClientReadModels.IgnoreQueryFilters().ToListAsync(cancellationToken);

        var contractsByClient = contracts.ToLookup(c => c.ClientId);
        var rowsById = rows.ToDictionary(row => row.Id);
        var seen = new HashSet<Guid>();

        foreach (var client in clients)
        {
            seen.Add(client.Id);
            var clientContracts = contractsByClient[client.Id].ToList();

            if (rowsById.TryGetValue(client.Id, out var existing))
            {
                Map(client, clientContracts, existing);
            }
            else
            {
                var fresh = new ClientReadModel();
                Map(client, clientContracts, fresh);
                context.ClientReadModels.Add(fresh);
            }
        }

        // Orphans: read rows whose source client no longer exists.
        foreach (var (id, row) in rowsById)
        {
            if (!seen.Contains(id))
            {
                context.ClientReadModels.Remove(row);
            }
        }
    }

    private static void Map(Client source, IReadOnlyCollection<Contract> contracts, ClientReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.Name = source.Name;
        row.Type = source.Type.ToString();
        row.ServiceType = source.ServiceType?.ToString();
        row.Tag = source.Tag;
        row.AgreementReference = source.AgreementReference;
        row.Notes = source.Notes;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;

        ActiveContractFields.Apply(row, contracts);
    }
}
