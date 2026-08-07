namespace NorthernLink.Shared.Kernel;

/// <summary>
/// The Internal (Northern Link) tenant's role set — see the northern-link-architecture skill,
/// Section 4.3. Lives in Shared.Kernel rather than the Identity library because a domain library
/// may reference <c>NorthernLink.Shared</c> and nothing else (enforced by
/// <c>ProjectReferenceRulesTests</c>): constants parked in Identity would be unreachable from
/// every other module's handlers. <see cref="SeedTenant"/> and <see cref="TenantType"/> are the
/// existing precedent for identity-adjacent constants living here.
///
/// These are <c>const string</c> rather than an enum so they drop straight into
/// <c>RequireRole(...)</c> and travel as-is in the JWT's "role" claim. Matching is ordinal and
/// case-sensitive, exactly as <c>RequireRole</c> compares — so "owner" must fail loudly at user
/// creation rather than silently 403 on every request later.
///
/// Client, Vendor/Partner and Consumer tenant roles (Section 4.3) are not here yet; they land
/// when those apps get their own accounts.
/// </summary>
public static class Roles
{
    public const string Owner = "Owner";
    public const string Dispatcher = "Dispatcher";

    /// <summary>"Supervisor/Manager" in Section 4.3 — the claim value is a single bare token.</summary>
    public const string Supervisor = "Supervisor";

    public const string Accountant = "Accountant";

    /// <summary>Section 4.3 lists this as future. Assignable now, but deliberately not yet in
    /// <see cref="BudgetAccess"/> — add it when the Board actually has accounts.</summary>
    public const string BoardMember = "BoardMember";

    public const string Driver = "Driver";

    /// <summary>
    /// The role literal every user created before this role model carries. Both creation paths
    /// hardcoded it, so it always meant "Owner" in practice. The RenameAdminRoleToOwner migration
    /// rewrites stored rows; this constant exists only so the AdminOnly policy keeps honouring
    /// access tokens minted before that migration ran (a 15-minute window at most). It is not a
    /// member of <see cref="Internal"/>, so <see cref="IsKnown"/> rejects it and no new user can
    /// be created with it. Delete once the migration has run in every environment.
    /// </summary>
    public const string LegacyAdmin = "Admin";

    public static readonly string[] Internal =
        [Owner, Dispatcher, Supervisor, Accountant, BoardMember, Driver];

    /// <summary>
    /// Financial oversight — architecture Sections 5.3 and 6.1. Superseded by Section 6.1's
    /// planned SuperUser claim when the Owner/Exec desktop app lands; until then a role list is
    /// the smaller thing that satisfies the requirement. Mirrored in <c>Budgeting/lib/roles.ts</c>.
    /// </summary>
    public static readonly string[] BudgetAccess = [Owner, Accountant];

    public static bool IsKnown(string role) => Array.IndexOf(Internal, role) >= 0;
}
