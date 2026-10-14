namespace NorthernLink.Notifications.Application.Integration;

/// <summary>
/// Notifications' replica of a platform user account, maintained by upserting
/// <c>UserChangedIntegrationEvent</c>s — never joined from the Identity module (domain libraries
/// never reference each other). A plain keyed row, not an aggregate, on the exact model of
/// Budgeting's <c>user_lookup</c>. It exists for one job: addressing the automated
/// hours-of-service compliance alerts to the people whose role makes them the audience
/// (<c>Roles.ComplianceAccess</c>, <c>Roles.DispatchAccess</c>) without a lookup across modules.
/// <para>
/// <b><see cref="FullName"/> is the display value, and <see cref="Email"/> is the fallback.</b>
/// A name is null for any account whose owner has not set one, so the alert's recipient record
/// falls back to the email — the one identifier guaranteed to be present.
/// </para>
/// <para>
/// <b><see cref="Role"/> is one role, not a list.</b> An Identity user holds exactly one role
/// (<c>User.Role</c>, validated against <c>Roles.Internal</c>) and the event carries it as one
/// string, so the replica stores one varchar rather than inventing a jsonb array the source
/// cannot fill. Audience selection is "role is in the policy's list", which reads the same either
/// way; if Identity ever grows multi-role accounts, widen this column with the event, not ahead
/// of it.
/// </para>
/// <para>
/// <b><see cref="IsActive"/> is a forward seam, not a live signal.</b> Identity has no
/// deactivation yet (<c>User</c> is create-and-edit only), so every event sets it true and the
/// backfill inserts true. The recipient query already filters on it so that the day Identity
/// raises a deactivation and the event carries the flag, a departed officer stops receiving
/// alerts by changing the mapper and the handler — never by adding a filter in a hurry here.
/// </para>
/// </summary>
public sealed class UserLookup
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string Email { get; set; } = null!;

    /// <summary>The user's own display name, or null when they have not set one.</summary>
    public string? FullName { get; set; }

    /// <summary>One of <c>Roles.Internal</c>, exactly as the token's role claim spells it.</summary>
    public string Role { get; set; } = null!;

    /// <summary>Whether the account may still receive alerts. Always true until Identity grows deactivation.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>What an alert addresses the person as: the name when set, else the email.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(FullName) ? Email : FullName;
}
