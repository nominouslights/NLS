using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Identity;
using NorthernLink.Shared.Kernel;
using NorthernLink.Identity.Domain.Users;
using NorthernLink.Identity.Domain.Users.Events;

namespace NorthernLink.Identity.Application.Auth;

/// <summary>
/// Identity's explicit domain-event → integration-event translation — the module's first, so
/// <c>identity.outbox_messages</c> stops being permanently empty. User creation maps to the
/// full-snapshot <c>UserChangedIntegrationEvent</c> so consumers (Budgeting's <c>user_lookup</c>)
/// maintain a replica by upsert.
/// <para>
/// Everything else stays internal (null) on purpose: refresh tokens and admin bootstrap tokens
/// are credentials, and putting either on a bus no module needs would be a security surface with
/// no consumer. Only the facts another module must know about a person travel — id, tenant,
/// email, role.
/// </para>
/// <para>
/// Creation and profile edits share one arm, and every field is read off the <b>aggregate</b>
/// rather than off the domain event. That is what makes "any change to a User publishes the whole
/// current snapshot" structural rather than a convention each new arm has to remember — a replica
/// that upserts the full row can never be left holding a half-applied change. See
/// <see cref="UserChangedIntegrationEvent"/> for what must happen here when the aggregate grows
/// an email change or a deactivation.
/// </para>
/// </summary>
public sealed class IdentityIntegrationEventMapper : IIntegrationEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, AggregateRoot aggregate) =>
        domainEvent switch
        {
            UserCreatedDomainEvent or UserProfileUpdatedDomainEvent when aggregate is User user =>
                new UserChangedIntegrationEvent(
                    user.Id,
                    user.TenantId,
                    user.Email,
                    user.Role,
                    user.FullName,
                    user.JobTitle),
            _ => null,
        };
}
