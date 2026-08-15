using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Identity;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Budgeting.Application.Abstractions;

namespace NorthernLink.Budgeting.Application.Integration;

/// <summary>
/// Keeps <c>budgeting.user_lookup</c> current from the Identity module's change stream, so a
/// budget code's owner and audit columns resolve to a readable email without a library reference.
/// Handlers run outside any HTTP request, so the event's tenant is pushed as the ambient tenant
/// for the write (RLS session variable). Delivery is at-least-once: the upsert is keyed on
/// UserId, so replays converge on the same row.
/// </summary>
public sealed class UserChangedIntegrationEventHandler(
    IUserLookupRepository repository,
    ILogger<UserChangedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<UserChangedIntegrationEvent>
{
    public async Task Handle(UserChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        using (AmbientTenant.Push(integrationEvent.TenantId))
        {
            await repository.UpsertAsync(
                new UserLookup
                {
                    UserId = integrationEvent.UserId,
                    TenantId = integrationEvent.TenantId,
                    Email = integrationEvent.Email,
                    Role = integrationEvent.Role,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                },
                cancellationToken);
        }

        logger.LogInformation(
            "Budgeting upserted user_lookup for user {UserId} of tenant {TenantId} ({EventId})",
            integrationEvent.UserId, integrationEvent.TenantId, integrationEvent.EventId);
    }
}
