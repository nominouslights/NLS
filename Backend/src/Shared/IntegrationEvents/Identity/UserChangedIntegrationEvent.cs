using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Identity;

/// <summary>
/// Published whenever a platform user account is created — routing key
/// <c>identity.user-changed</c>. Named "changed" rather than "created" so consumers maintain a
/// replica by upserting on <see cref="UserId"/> (idempotent under at-least-once delivery) and so
/// the contract does not have to change the day accounts become editable. Budgeting consumes it
/// to keep its <c>user_lookup</c> table current, which is how a budget code names an accountable
/// owner without a library reference to Identity.
/// <para>
/// <b>Today this fires on create only.</b> <c>Identity.User</c> is a create-only aggregate — it
/// has no rename, no deactivate and no delete — so <c>UserCreatedDomainEvent</c> is the single
/// event the mapper has to translate. Whoever adds <c>User.ChangeEmail</c>, a role change or a
/// deactivation must raise a domain event <em>and</em> add it to
/// <c>IdentityIntegrationEventMapper</c>'s switch, or every replica silently goes stale with no
/// error anywhere.
/// </para>
/// <para>
/// <see cref="Role"/> travels as a string (one of <c>Roles.Internal</c>) because integration
/// events never reference another module's types. <see cref="TenantId"/> is part of the payload
/// because handlers run outside any HTTP request. There is no display name on the wire because
/// <c>Identity.User</c> has none — email is the only human-readable identifier a user has.
/// </para>
/// </summary>
public sealed record UserChangedIntegrationEvent(
    Guid UserId,
    Guid TenantId,
    string Email,
    string Role) : IntegrationEvent;
