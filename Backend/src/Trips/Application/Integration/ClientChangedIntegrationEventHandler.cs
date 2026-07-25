using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Clients;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Abstractions;

namespace NorthernLink.Trips.Application.Integration;

/// <summary>
/// Keeps <c>trips.client_lookup</c> current from the Clients module's change stream, so
/// trip/template client snapshots resolve without a library reference. Handlers run
/// outside any HTTP request, so the event's tenant is pushed as the ambient tenant for
/// the write (RLS session variable). Delivery is at-least-once: the upsert is keyed on
/// ClientId, so replays converge on the same row.
/// </summary>
public sealed class ClientChangedIntegrationEventHandler(
    IClientLookupRepository repository,
    ILogger<ClientChangedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<ClientChangedIntegrationEvent>
{
    public async Task Handle(ClientChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        using (AmbientTenant.Push(integrationEvent.TenantId))
        {
            await repository.UpsertAsync(
                new ClientLookup
                {
                    ClientId = integrationEvent.ClientId,
                    TenantId = integrationEvent.TenantId,
                    Name = integrationEvent.Name,
                    Type = integrationEvent.Type,
                    ServiceType = integrationEvent.ServiceType,
                    Tag = integrationEvent.Tag,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                },
                cancellationToken);
        }

        logger.LogInformation(
            "Trips upserted client_lookup for client {ClientId} of tenant {TenantId} ({EventId})",
            integrationEvent.ClientId, integrationEvent.TenantId, integrationEvent.EventId);
    }
}
