namespace NorthernLink.Budgeting.Application.Integration;

/// <summary>
/// Budgeting's replica of a platform user account, maintained by upserting
/// <c>UserChangedIntegrationEvent</c>s — never joined from the Identity module (domain libraries
/// never reference each other). A plain keyed row, not an aggregate. It exists so a budget code
/// can name an accountable owner, and so <c>created_by</c> / <c>modified_by</c> resolve to
/// something a person can read.
/// <para>
/// <b><see cref="FullName"/> is the display value, and <see cref="Email"/> is the fallback.</b>
/// A name is null for any account whose owner has not set one — which is every account created
/// before Identity grew profiles — so every read path has to cope with that, and email is the
/// one identifier guaranteed to be present. Email is also what disambiguates two people with
/// similar names, which is why both are kept rather than collapsed into one display column here.
/// </para>
/// <para>
/// The job title deliberately does not travel this far. It rides the integration event because
/// Identity publishes a full snapshot, but nothing in Budgeting renders it, and the discipline
/// for this replica is to carry only what it displays.
/// </para>
/// <para>
/// <b>Known gap:</b> <c>Identity.User</c> is create-only — no deactivation, no deletion — so this
/// replica lists every account that has ever existed and can never shrink. Acceptable for a
/// handful of internal accounts; a real problem the day people leave, and the fix belongs in
/// Identity (a deactivation event), not in a filter here.
/// </para>
/// </summary>
public sealed class UserLookup
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string Email { get; set; } = null!;

    /// <summary>The user's own display name, or null when they have not set one.</summary>
    public string? FullName { get; set; }

    public string Role { get; set; } = null!;
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
