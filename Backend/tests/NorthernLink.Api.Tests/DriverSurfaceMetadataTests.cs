using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NorthernLink.Billing.Infrastructure.Endpoints;
using NorthernLink.Clients.Infrastructure.Endpoints;
using NorthernLink.Drivers.Application.Drivers.SelfAccess;
using NorthernLink.Drivers.Infrastructure.Endpoints;
using NorthernLink.Fleet.Infrastructure.Endpoints;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Storage;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Infrastructure.Endpoints;
using Xunit;

namespace NorthernLink.Api.Tests;

/// <summary>
/// The regression test for the Driver Field App security fix — modelled on
/// <c>BudgetingEndpointMetadataTests</c>, but spanning five modules, which is why it lives in the
/// gateway's own test project (the only one that references them all).
///
/// <para>
/// <b>The hole this exists to keep closed.</b> Every group under <c>/api/trips</c>,
/// <c>/api/fleet</c>, <c>/api/clients</c>, <c>/api/billing</c> and <c>/api/drivers</c> used to
/// carry a bare <c>.RequireAuthorization()</c> — authenticated, no policy. A Driver-role token
/// satisfies that. One login intended for a tablet in a van could therefore create routes, delete
/// stops, register drivers, write work orders and read every invoice in the business. The policy
/// tests next door would not have noticed: they prove what each policy admits, never that any
/// endpoint carries one.
/// </para>
///
/// <para>
/// <b>The rule enforced here:</b> no endpoint on those prefixes may carry an authorize with no
/// policy. That is what fails the build if someone maps a new endpoint outside a group, or
/// "temporarily" drops a policy back to a bare authorize.
/// </para>
///
/// <para>
/// <b>And the trap it also guards.</b> <c>Roles.DriverAccess</c> CONTAINS <c>Driver</c>, so
/// attaching it to a dispatch group widens that group instead of fixing it — the tempting
/// one-line "fix" is strictly worse than the bug. <see cref="Dispatch_only_routes_never_carry_the
/// _wider_DriverAccess_policy"/> pins the specific routes that must stay dispatch-only.
/// </para>
/// </summary>
public class DriverSurfaceMetadataTests : IAsyncLifetime
{
    /// <summary>
    /// Prefixes a Driver-role token could reach through the old bare authorize. An endpoint under
    /// any of these must name a policy.
    /// </summary>
    private static readonly string[] GuardedPrefixes =
        ["/api/trips", "/api/fleet", "/api/clients", "/api/billing", "/api/drivers"];

    private WebApplication _app = null!;
    private List<RouteEndpoint> _endpoints = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();

        // The handler delegates take these from DI; without registrations the endpoint builder
        // cannot classify the parameters (service vs. body) and building the endpoints throws.
        // Nothing is ever resolved — the app never runs, so null instances are fine.
        builder.Services.AddScoped<ISender, Sender>();
        builder.Services.AddScoped<ITenantContext, StubTenantContext>();
        builder.Services.AddScoped<ICurrentActor, StubCurrentActor>();
        builder.Services.AddScoped<IDriverSelfAccess>(_ => null!);
        builder.Services.AddScoped<IObjectStorage>(_ => null!);
        builder.Services.AddScoped<NorthernLink.Drivers.Application.Abstractions.IDriverCredentialRepository>(_ => null!);

        _app = builder.Build();
        _app.MapTripsEndpoints();
        _app.MapFleetEndpoints();
        _app.MapClientsEndpoints();
        _app.MapBillingEndpoints();
        _app.MapDriversEndpoints();

        // Materializing DataSources.Endpoints builds the endpoints without running the host.
        _endpoints = ((IEndpointRouteBuilder)_app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        await Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    /// <summary>
    /// Route patterns as ASP.NET builds them, minus a trailing slash. <c>group.MapGet("")</c>
    /// produces "/api/trips/" while <c>group.MapGet("{id:guid}")</c> produces
    /// "/api/trips/{id:guid}" — normalizing here keeps the InlineData below readable as the URLs
    /// a client actually calls.
    /// </summary>
    private static string Pattern(RouteEndpoint endpoint) =>
        (endpoint.RoutePattern.RawText ?? "(no pattern)").TrimEnd('/');

    private static string Describe(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
        return $"{string.Join("|", methods)} {Pattern(endpoint)}";
    }

    private IEnumerable<RouteEndpoint> Guarded() =>
        _endpoints.Where(endpoint =>
            GuardedPrefixes.Any(prefix =>
                endpoint.RoutePattern.RawText?.StartsWith(prefix, StringComparison.Ordinal) == true));

    [Fact]
    public void No_endpoint_on_a_driver_reachable_prefix_carries_a_bare_authorize()
    {
        var bare = Guarded()
            .Where(endpoint =>
                endpoint.Metadata.GetMetadata<IAuthorizeData>() is { } authorize
                && string.IsNullOrEmpty(authorize.Policy)
                && string.IsNullOrEmpty(authorize.Roles))
            .Select(Describe)
            .Order()
            .ToList();

        Assert.True(
            bare.Count == 0,
            "These endpoints carry an authorize with no policy, which any authenticated account " +
            $"— a Driver token included — satisfies: {string.Join(", ", bare)}. Map them onto a " +
            "group carrying DispatchAccess, or onto the driver-facing sibling group carrying " +
            "DriverAccess if they are genuinely part of the Driver Field App's surface.");
    }

    [Fact]
    public void No_endpoint_on_a_driver_reachable_prefix_is_left_unauthenticated()
    {
        var unprotected = Guarded()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAuthorizeData>() is null)
            .Select(Describe)
            .Order()
            .ToList();

        Assert.True(
            unprotected.Count == 0,
            $"These endpoints carry no authorization metadata at all: {string.Join(", ", unprotected)}.");
    }

    [Fact]
    public void Every_policy_named_on_these_prefixes_is_one_of_the_two_expected_ones()
    {
        // Catches a typo'd or invented policy name, which fails only on the first real request
        // to that endpoint — in production, not at startup.
        var unexpected = Guarded()
            .Select(endpoint => endpoint.Metadata.GetMetadata<IAuthorizeData>()?.Policy)
            .Where(policy => policy is not null
                && policy != AuthorizationPolicies.DispatchAccess
                && policy != AuthorizationPolicies.DriverAccess)
            .Distinct()
            .ToList();

        Assert.True(
            unexpected.Count == 0,
            $"Unexpected policies on the driver-reachable prefixes: {string.Join(", ", unexpected)}.");
    }

    /// <summary>
    /// The specific routes that must never gain the wider DriverAccess policy. Each one is a
    /// write a driver must not be able to make, or a read a driver must not be able to take.
    /// </summary>
    [Theory]
    [InlineData("POST", "/api/trips/routes")]
    [InlineData("PUT", "/api/trips/stops/{id:guid}")]
    [InlineData("POST", "/api/trips/schedule-templates")]
    [InlineData("POST", "/api/trips/shipments")]
    [InlineData("GET", "/api/trips/riders")]
    [InlineData("POST", "/api/trips")]
    [InlineData("POST", "/api/trips/manifests")]
    [InlineData("POST", "/api/fleet/vehicles")]
    [InlineData("POST", "/api/fleet/work-orders")]
    [InlineData("POST", "/api/fleet/pm-plans")]
    [InlineData("POST", "/api/fleet/shops")]
    [InlineData("DELETE", "/api/fleet/inspections/{id:guid}")]
    [InlineData("POST", "/api/clients")]
    [InlineData("GET", "/api/billing/invoices")]
    [InlineData("GET", "/api/billing/billable-trips")]
    [InlineData("POST", "/api/drivers")]
    [InlineData("POST", "/api/drivers/{driverId:guid}/credentials")]
    [InlineData("POST", "/api/drivers/{driverId:guid}/clearances")]
    [InlineData("POST", "/api/drivers/{id:guid}/user")]
    [InlineData("DELETE", "/api/drivers/{id:guid}/user")]
    public void Dispatch_only_routes_never_carry_the_wider_DriverAccess_policy(string method, string pattern)
    {
        var endpoint = Endpoint(method, pattern);

        var policy = endpoint.Metadata.GetMetadata<IAuthorizeData>()?.Policy;

        Assert.Equal(AuthorizationPolicies.DispatchAccess, policy);
    }

    /// <summary>
    /// The other direction: the routes the Driver Field App genuinely needs. If one of these
    /// silently reverts to DispatchAccess the app 403s in the field, which is a worse failure
    /// mode than a build break here.
    /// </summary>
    [Theory]
    [InlineData("GET", "/api/trips")]
    [InlineData("POST", "/api/trips/{id:guid}/status")]
    [InlineData("GET", "/api/trips/manifests")]
    [InlineData("POST", "/api/fleet/inspections")]
    [InlineData("GET", "/api/drivers/{driverId:guid}/hos")]
    [InlineData("POST", "/api/drivers/{driverId:guid}/hos")]
    [InlineData("GET", "/api/drivers/{driverId:guid}/credentials")]
    [InlineData("GET", "/api/drivers/{driverId:guid}/clearances")]
    [InlineData("GET", "/api/drivers/me")]
    public void Driver_facing_routes_carry_DriverAccess(string method, string pattern)
    {
        var endpoint = Endpoint(method, pattern);

        var policy = endpoint.Metadata.GetMetadata<IAuthorizeData>()?.Policy;

        Assert.Equal(AuthorizationPolicies.DriverAccess, policy);
    }

    private RouteEndpoint Endpoint(string method, string pattern)
    {
        var endpoint = _endpoints.SingleOrDefault(e =>
            Pattern(e) == pattern
            && e.Metadata.GetMetadata<HttpMethodMetadata>() is { } httpMethods
            && httpMethods.HttpMethods.Contains(method));

        Assert.True(endpoint is not null, $"Expected a mapped endpoint {method} {pattern}.");
        return endpoint;
    }

    private sealed class StubTenantContext : ITenantContext
    {
        public Guid? TenantId => null;

        public TenantType? TenantType => null;
    }

    private sealed class StubCurrentActor : ICurrentActor
    {
        public Guid? UserId => null;

        public string? Email => null;

        public IReadOnlyCollection<string> Roles => [];
    }
}
