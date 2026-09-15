using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NorthernLink.Api.Auth;
using NorthernLink.Identity.Infrastructure.Auth;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Api.Tests;

/// <summary>
/// Evaluates the gateway's real policies against hand-built principals. No host, no database:
/// <see cref="AuthorizationPolicyRegistration"/> is registered on a bare ServiceCollection and
/// asked directly.
///
/// Scope limit worth knowing: this covers the policies, not the bearer handler. The other half
/// of role authorization — Program.cs setting <c>MapInboundClaims = false</c> together with
/// <c>RoleClaimType = JwtAccessTokenIssuer.RoleClaimType</c>, without which "role" is renamed to
/// the ClaimTypes.Role URI before any policy sees it and every RequireRole below silently 403s —
/// lives in the handler configuration and is not exercised here.
/// </summary>
public class AuthorizationPolicyTests
{
    private readonly IAuthorizationService _authorization = new ServiceCollection()
        .AddLogging()
        .AddAuthorization(AuthorizationPolicyRegistration.Add)
        .BuildServiceProvider()
        .GetRequiredService<IAuthorizationService>();

    /// <summary>
    /// A principal shaped exactly as the bearer handler produces one: claims kept literal, and
    /// the role claim type named explicitly rather than relying on ClaimsIdentity's default.
    /// </summary>
    private static ClaimsPrincipal PrincipalWithRole(string role) =>
        new(new ClaimsIdentity(
            [
                new Claim(JwtAccessTokenIssuer.RoleClaimType, role),
                new Claim(JwtAccessTokenIssuer.TenantIdClaimType, SeedTenant.Id.ToString()),
            ],
            authenticationType: "test",
            nameType: null,
            roleType: JwtAccessTokenIssuer.RoleClaimType));

    private async Task<bool> Allows(string policy, string role) =>
        (await _authorization.AuthorizeAsync(PrincipalWithRole(role), resource: null, policy)).Succeeded;

    [Theory]
    [InlineData(Roles.Owner)]
    [InlineData(Roles.Accountant)]
    public async Task BudgetAccess_admits_the_financial_roles(string role) =>
        Assert.True(await Allows(AuthorizationPolicies.BudgetAccess, role));

    /// <summary>
    /// US-6.0.1's acceptance criterion, server side: a Dispatcher account must be rejected from
    /// budgeting. Supervisor is named in the same criterion; the remaining roles are here so that
    /// widening BudgetAccess is a deliberate edit to this list rather than a silent side effect.
    /// The frontend has its own counterpart test (Budgeting/lib/roles.test.ts), but it is a UX
    /// gate: the security boundary is this policy, and the whole <c>/api/budgeting</c> group now
    /// carries it (<c>BudgetingEndpoints.MapBudgetingEndpoints</c>). That attachment is pinned by
    /// <c>BudgetingEndpointMetadataTests</c> in the Budgeting test project — deliberately not
    /// here, because these tests evaluate policies in isolation and would stay green if every
    /// endpoint lost its RequireAuthorization.
    /// </summary>
    [Theory]
    [InlineData(Roles.Dispatcher)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Driver)]
    [InlineData(Roles.BoardMember)]
    public async Task BudgetAccess_denies_every_other_role(string role) =>
        Assert.False(await Allows(AuthorizationPolicies.BudgetAccess, role));

    [Fact]
    public async Task BudgetAccess_never_accepts_the_legacy_Admin_literal()
    {
        // Nothing predating the role model was ever granted budget access, so unlike AdminOnly
        // there is no transitional window to honour here.
        Assert.False(await Allows(AuthorizationPolicies.BudgetAccess, Roles.LegacyAdmin));
    }

    [Theory]
    [InlineData(Roles.Owner)]
    [InlineData(Roles.Dispatcher)]
    [InlineData(Roles.Supervisor)]
    public async Task DispatchAccess_admits_the_dispatch_capable_roles(string role) =>
        Assert.True(await Allows(AuthorizationPolicies.DispatchAccess, role));

    /// <summary>
    /// The /api/notifications boundary: templates and passenger emails are dispatch work.
    /// The remaining internal roles are enumerated so that widening DispatchAccess is a
    /// deliberate edit to this list rather than a silent side effect.
    /// </summary>
    [Theory]
    [InlineData(Roles.Accountant)]
    [InlineData(Roles.BoardMember)]
    [InlineData(Roles.Driver)]
    public async Task DispatchAccess_denies_every_other_role(string role) =>
        Assert.False(await Allows(AuthorizationPolicies.DispatchAccess, role));

    [Fact]
    public async Task DispatchAccess_never_accepts_the_legacy_Admin_literal()
    {
        // The notifications group gained this policy after RenameAdminRoleToOwner ran, so
        // unlike AdminOnly there is no transitional window to honour here.
        Assert.False(await Allows(AuthorizationPolicies.DispatchAccess, Roles.LegacyAdmin));
    }

    /// <summary>
    /// DriverAccess is DispatchAccess widened with Driver, not a separate branch: dispatch staff
    /// must be able to do everything a driver can (working a driver's screen over the phone is
    /// routine), so all four roles are admitted.
    /// </summary>
    [Theory]
    [InlineData(Roles.Driver)]
    [InlineData(Roles.Dispatcher)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Owner)]
    public async Task DriverAccess_admits_the_field_and_dispatch_roles(string role) =>
        Assert.True(await Allows(AuthorizationPolicies.DriverAccess, role));

    /// <summary>
    /// The back-office roles have no business on a duty log or a DVIR. Enumerated so that
    /// widening DriverAccess is a deliberate edit to <c>Roles.DriverAccess</c> rather than a
    /// silent side effect.
    /// </summary>
    [Theory]
    [InlineData(Roles.Accountant)]
    [InlineData(Roles.BoardMember)]
    public async Task DriverAccess_denies_the_back_office_roles(string role) =>
        Assert.False(await Allows(AuthorizationPolicies.DriverAccess, role));

    [Fact]
    public async Task DriverAccess_never_accepts_the_legacy_Admin_literal()
    {
        // The driver-facing groups were carved out long after RenameAdminRoleToOwner ran, so
        // unlike AdminOnly there is no transitional window to honour here.
        Assert.False(await Allows(AuthorizationPolicies.DriverAccess, Roles.LegacyAdmin));
    }

    /// <summary>
    /// The relationship the group restructure depends on. DriverAccess CONTAINS DispatchAccess,
    /// which is why adding it to an existing dispatch group would widen that group rather than
    /// narrow it — driver-facing routes get their own sibling MapGroup instead. If this ever
    /// stops holding, re-read every <c>.RequireAuthorization(DriverAccess)</c> call site.
    /// </summary>
    [Fact]
    public void DriverAccess_is_a_strict_superset_of_DispatchAccess()
    {
        Assert.All(Roles.DispatchAccess, role => Assert.Contains(role, Roles.DriverAccess));
        Assert.Contains(Roles.Driver, Roles.DriverAccess);
        Assert.DoesNotContain(Roles.Driver, Roles.DispatchAccess);
    }

    [Fact]
    public async Task AdminOnly_admits_Owner() =>
        Assert.True(await Allows(AuthorizationPolicies.AdminOnly, Roles.Owner));

    [Fact]
    public async Task AdminOnly_still_admits_the_legacy_Admin_literal()
    {
        // Transitional: access tokens minted just before RenameAdminRoleToOwner ran stay valid
        // for up to their 15-minute lifetime. Remove with Roles.LegacyAdmin.
        Assert.True(await Allows(AuthorizationPolicies.AdminOnly, Roles.LegacyAdmin));
    }

    [Theory]
    [InlineData(Roles.Dispatcher)]
    [InlineData(Roles.Accountant)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Driver)]
    public async Task AdminOnly_denies_non_owner_roles(string role) =>
        Assert.False(await Allows(AuthorizationPolicies.AdminOnly, role));

    [Theory]
    [InlineData(AuthorizationPolicies.AdminOnly)]
    [InlineData(AuthorizationPolicies.BudgetAccess)]
    [InlineData(AuthorizationPolicies.DispatchAccess)]
    [InlineData(AuthorizationPolicies.DriverAccess)]
    public async Task Unauthenticated_principals_satisfy_nothing(string policy)
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await _authorization.AuthorizeAsync(anonymous, resource: null, policy);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(AuthorizationPolicies.AdminOnly)]
    [InlineData(AuthorizationPolicies.BudgetAccess)]
    [InlineData(AuthorizationPolicies.DispatchAccess)]
    [InlineData(AuthorizationPolicies.DriverAccess)]
    public void Every_policy_name_constant_resolves_to_a_registered_policy(string policy)
    {
        // A name constant with no matching AddPolicy call throws only when a request first hits
        // the endpoint carrying it — in production, not at startup.
        var options = new AuthorizationOptions();
        AuthorizationPolicyRegistration.Add(options);

        Assert.NotNull(options.GetPolicy(policy));
    }
}
