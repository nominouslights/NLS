namespace NorthernLink.Budgeting.Application.Integration;

/// <summary>
/// Budgeting's replica of a platform user account, maintained by upserting
/// <c>UserChangedIntegrationEvent</c>s — never joined from the Identity module (domain libraries
/// never reference each other). A plain keyed row, not an aggregate. It exists so a budget code
/// can name an accountable owner, and so <c>created_by</c> / <c>modified_by</c> resolve to
/// something a person can read.
/// <para>
/// <b>Email is the display value because there is nothing else.</b> <c>Identity.User</c> has no
/// name field — email is the only human-readable identifier a user has. The day Identity grows
/// one, it joins the integration event and lands here.
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
    public string Role { get; set; } = null!;
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
