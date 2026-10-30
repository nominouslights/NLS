using Microsoft.AspNetCore.Authorization;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Api.Auth;

/// <summary>
/// The gateway's authorization policies, extracted from Program.cs so they can be evaluated in
/// a unit test against a bare ServiceCollection. Booting the real host for this is not an
/// option: Program.cs reads four required environment variables at startup and needs Postgres
/// and RabbitMQ before a single policy could be asked a question.
///
/// Policies are matched against the "role" claim, which reaches here intact only because the
/// bearer handler sets both <c>MapInboundClaims = false</c> and
/// <c>RoleClaimType = JwtAccessTokenIssuer.RoleClaimType</c> (see Program.cs). Dropping either
/// silently 403s every RequireRole below — that half is not covered by these tests, since it
/// lives in the handler rather than the policy.
/// </summary>
public static class AuthorizationPolicyRegistration
{
    public static void Add(AuthorizationOptions options)
    {
        // Roles.LegacyAdmin is transitional. The RenameAdminRoleToOwner migration rewrites stored
        // rows, but access tokens minted just before it ran still carry "Admin" for up to their
        // 15-minute lifetime. Drop it once that migration has run everywhere.
        options.AddPolicy(
            AuthorizationPolicies.AdminOnly,
            policy => policy.RequireRole(Roles.Owner, Roles.LegacyAdmin));

        // Budget and financial control — architecture Sections 5.3 and 6.1. Deliberately does not
        // accept LegacyAdmin: nothing predating the role model has ever been granted budget access,
        // so there is no compatibility to preserve here.
        //
        // Attached to the whole /api/budgeting group (BudgetingEndpoints.cs) — periods and codes
        // today, everything added there tomorrow. This is the real boundary: the Budgeting
        // console's RoleGate reads an unverified client-side JWT decode and is a UX gate only,
        // so nothing may treat passing it as proof of anything.
        options.AddPolicy(
            AuthorizationPolicies.BudgetAccess,
            policy => policy.RequireRole(Roles.BudgetAccess));

        // Dispatch operations — attached to the whole /api/notifications group
        // (NotificationsEndpoints.cs): authoring email templates and emailing passengers is
        // dispatch work, not something every authenticated account may do. Like BudgetAccess,
        // it does not accept LegacyAdmin: the group gained this policy after the
        // RenameAdminRoleToOwner migration, so no "Admin" token ever legitimately held it.
        options.AddPolicy(
            AuthorizationPolicies.DispatchAccess,
            policy => policy.RequireRole(Roles.DispatchAccess));

        // The Driver Field App's surface — DispatchAccess widened with Driver. Like the two
        // policies above it does not accept LegacyAdmin: the driver-facing groups were carved out
        // long after RenameAdminRoleToOwner ran, so no "Admin" token ever held them.
        //
        // Attached ONLY to the driver-facing sibling groups (see Roles.DriverAccess). It is wider
        // than DispatchAccess, so it must never be bolted onto a dispatch group "to let drivers
        // in" — that opens every write on that prefix to every driver.
        options.AddPolicy(
            AuthorizationPolicies.DriverAccess,
            policy => policy.RequireRole(Roles.DriverAccess));

        // Hours-of-service (NSC 9 / M.R. 72/2007). Three policies, none attached to an endpoint
        // yet — the Drivers ledger and compliance endpoints that carry them land in later PRs.
        // None accepts LegacyAdmin: the role model predates all of them.
        //
        // ComplianceAccess is deliberately narrow (Owner + ComplianceOfficer): it guards the only
        // overrides past the hours gate. HosAccess and DriverRosterAccess are the existing
        // DriverAccess / DispatchAccess lists widened with ComplianceOfficer — new policies rather
        // than edits to those lists, so no existing group's membership changes here.
        options.AddPolicy(
            AuthorizationPolicies.ComplianceAccess,
            policy => policy.RequireRole(Roles.ComplianceAccess));

        options.AddPolicy(
            AuthorizationPolicies.HosAccess,
            policy => policy.RequireRole(Roles.HosAccess));

        options.AddPolicy(
            AuthorizationPolicies.DriverRosterAccess,
            policy => policy.RequireRole(Roles.DriverRosterAccess));
    }
}
