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
        // No endpoint carries this policy yet — there are no budgeting endpoints until Stage 6.1.
        // It is registered and tested now so that story attaches an existing policy instead of
        // inventing a parallel one, and so the Budgeting client's role list has a server-side
        // counterpart to be checked against rather than being the only gate in the system.
        options.AddPolicy(
            AuthorizationPolicies.BudgetAccess,
            policy => policy.RequireRole(Roles.BudgetAccess));
    }
}
