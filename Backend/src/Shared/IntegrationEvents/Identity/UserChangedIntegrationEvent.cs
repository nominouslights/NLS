using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Identity;

/// <summary>
/// Published whenever a platform user account is created or edited — routing key
/// <c>identity.user-changed</c>. Named "changed" rather than "created" so consumers maintain a
/// replica by upserting on <see cref="UserId"/> (idempotent under at-least-once delivery) and so
/// the contract did not have to change the day accounts became editable. Budgeting consumes it
/// to keep its <c>user_lookup</c> table current, which is how a budget code names an accountable
/// owner without a library reference to Identity.
/// <para>
/// It fires on account creation and on a profile edit, and it is always a <b>full snapshot</b> —
/// the mapper reads every field off the aggregate, never off the domain event. Whoever adds an
/// email change, a role change or a deactivation must raise a domain event <em>and</em> add it to
/// <c>IdentityIntegrationEventMapper</c>'s switch, or every replica silently goes stale with no
/// error anywhere.
/// </para>
/// <para>
/// <see cref="Role"/> travels as a string (one of <c>Roles.Internal</c>) because integration
/// events never reference another module's types. <see cref="TenantId"/> is part of the payload
/// because handlers run outside any HTTP request. <see cref="FullName"/> and
/// <see cref="JobTitle"/> are null for any user who has not set a profile, which is every account
/// created before profiles existed — consumers fall back to <see cref="Email"/>, the only
/// identifier guaranteed to be there.
/// </para>
/// <para>
/// <b>Wire compatibility.</b> Payloads already sitting in <c>identity.outbox_messages</c> were
/// serialized before the profile fields existed. System.Text.Json binds this positional record
/// through its constructor and lets a missing member fall to its default, so those rows still
/// deserialize, with both profile fields null. Two rules keep that true, and
/// <c>UserChangedIntegrationEventWireCompatTests</c> pins them: never mark a member
/// <c>required</c> or <c>[JsonRequired]</c> (that turns every historical row into a
/// <c>JsonException</c>, which the polling consumer parks as Failed with no retry), and only ever
/// append — renaming a member silently nulls it on old and new rows alike.
/// </para>
/// </summary>
public sealed record UserChangedIntegrationEvent(
    Guid UserId,
    Guid TenantId,
    string Email,
    string Role,
    string? FullName,
    string? JobTitle) : IntegrationEvent;
