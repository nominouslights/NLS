using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NorthernLink.Notifications.Infrastructure.Endpoints;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>
/// Pins the module's auth surface without starting a server: the real
/// <c>MapNotificationsEndpoints</c> is mapped onto a throwaway (never-run) WebApplication and
/// the resulting endpoint metadata is asserted directly — same technique as
/// <c>IdentityEndpointMetadataTests</c>.
/// <para>
/// The gateway's <c>AuthorizationPolicyTests</c> prove DispatchAccess admits
/// Owner/Dispatcher/Supervisor and denies Accountant, BoardMember and Driver. They cannot see
/// whether anything carries the policy. Drop the
/// <c>.RequireAuthorization(DispatchAccess)</c> from the group and passenger emails, template
/// authoring and the whole dispatch history open to every authenticated account — with the
/// policy tests still green. These tests fail instead.
/// </para>
/// </summary>
public class NotificationsEndpointMetadataTests : IAsyncLifetime
{
    /// <summary>
    /// Endpoints deliberately reachable without authorization. Empty, and it must stay that
    /// way: everything under /api/notifications sends or reads passenger-facing mail. An entry
    /// here is a decision, not a default.
    /// </summary>
    private static readonly HashSet<string> AnonymousByDesign = [];

    private WebApplication _app = null!;
    private List<RouteEndpoint> _endpoints = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();

        // The handler delegates take ISender / ITenantContext from DI; without registrations
        // the endpoint builder cannot classify those parameters (services vs. body) and
        // building the endpoints throws. The services are never resolved — the app never runs.
        builder.Services.AddScoped<ISender, Sender>();
        builder.Services.AddScoped<ITenantContext, StubTenantContext>();

        _app = builder.Build();
        _app.MapNotificationsEndpoints();

        // Materializing DataSources.Endpoints builds the endpoints without running the host.
        _endpoints = ((IEndpointRouteBuilder)_app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        await Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    private RouteEndpoint Endpoint(string method, string pattern)
    {
        var endpoint = _endpoints.SingleOrDefault(e =>
            e.RoutePattern.RawText == pattern
            && e.Metadata.GetMetadata<HttpMethodMetadata>() is { } httpMethods
            && httpMethods.HttpMethods.Contains(method));

        Assert.True(endpoint is not null, $"Expected a mapped endpoint {method} {pattern}.");
        return endpoint;
    }

    [Theory]
    [InlineData("GET", "/api/notifications/templates")]
    [InlineData("POST", "/api/notifications/templates")]
    [InlineData("POST", "/api/notifications/templates/preview")]
    [InlineData("GET", "/api/notifications/templates/{id:guid}")]
    [InlineData("PUT", "/api/notifications/templates/{id:guid}")]
    [InlineData("POST", "/api/notifications/templates/{id:guid}/activate")]
    [InlineData("POST", "/api/notifications/templates/{id:guid}/deactivate")]
    [InlineData("POST", "/api/notifications/emails/trip-pickup")]
    [InlineData("POST", "/api/notifications/emails/trip-pickup/report-preview")]
    [InlineData("POST", "/api/notifications/emails/client-accruals")]
    [InlineData("POST", "/api/notifications/emails/client-accruals/preview")]
    [InlineData("POST", "/api/notifications/emails/booking-passes")]
    [InlineData("POST", "/api/notifications/emails/booking-passes/preview")]
    [InlineData("GET", "/api/notifications/emails")]
    public void Every_notifications_endpoint_carries_the_DispatchAccess_policy(string method, string pattern)
    {
        var endpoint = Endpoint(method, pattern);

        var authorizeData = endpoint.Metadata.GetMetadata<IAuthorizeData>();

        Assert.NotNull(authorizeData);
        Assert.Equal(AuthorizationPolicies.DispatchAccess, authorizeData.Policy);
    }

    [Fact]
    public void No_mapped_endpoint_is_left_without_authorization_metadata()
    {
        // The blanket guard: a new endpoint mapped outside the group (or onto a group that lost
        // its RequireAuthorization) is caught here even though no InlineData above names it.
        var unprotected = _endpoints
            .Where(e => e.Metadata.GetMetadata<IAuthorizeData>() is null)
            .Select(e => e.RoutePattern.RawText ?? "(no pattern)")
            .Where(pattern => !AnonymousByDesign.Contains(pattern))
            .ToList();

        Assert.True(
            unprotected.Count == 0,
            "These /api/notifications endpoints carry no authorization metadata: " +
            $"{string.Join(", ", unprotected)}. Add them to the group that carries " +
            "DispatchAccess, or allow-list them explicitly as anonymous by design.");
    }

    [Fact]
    public void The_pinned_route_list_covers_every_mapped_endpoint()
    {
        // Guards the guard: without this, an endpoint added to the group would be silently
        // absent from the Theory above (which asserts only the routes it names).
        //
        // It did exactly that: the two emails/booking-passes routes arrived without being named
        // above, and this count sat red until someone reconciled it. When it fails, add the new
        // route to the Theory and then raise this number — raising it alone re-greens the suite
        // while leaving the new endpoint's policy unasserted, which is the failure this guards.
        Assert.Equal(14, _endpoints.Count);
    }

    private sealed class StubTenantContext : ITenantContext
    {
        public Guid? TenantId => null;

        public TenantType? TenantType => null;
    }
}
