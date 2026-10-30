using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Identity;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Notifications.Application.Abstractions;

namespace NorthernLink.Notifications.Application.Integration;

/// <summary>
/// Keeps <c>notifications.user_lookup</c> current from the Identity module's change stream, so
/// a compliance alert can find every Owner, ComplianceOfficer, Dispatcher and Supervisor in the
/// tenant without a library reference. Handlers run outside any HTTP request, so the event's
/// tenant is pushed as the ambient tenant for the write (RLS session variable). Delivery is
/// at-least-once: the upsert is keyed on (UserId, TenantId), so replays converge on one row.
/// Every column the event carries is reassigned, nulls included — the event is a full snapshot.
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
                    FullName = integrationEvent.FullName,
                    Role = integrationEvent.Role,
                    // Identity has no deactivation yet, so the event carries no flag; see
                    // UserLookup.IsActive for why the column exists anyway.
                    IsActive = true,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                },
                cancellationToken);
        }

        logger.LogInformation(
            "Notifications upserted user_lookup for user {UserId} of tenant {TenantId} ({EventId})",
            integrationEvent.UserId, integrationEvent.TenantId, integrationEvent.EventId);
    }
}
