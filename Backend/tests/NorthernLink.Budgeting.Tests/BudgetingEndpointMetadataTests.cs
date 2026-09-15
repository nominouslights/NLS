using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NorthernLink.Budgeting.Infrastructure.Endpoints;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// Pins the module's auth surface without starting a server: the real
/// <c>MapBudgetingEndpoints</c> is mapped onto a throwaway (never-run) WebApplication and the
/// resulting endpoint metadata is asserted directly — same technique as
/// <c>IdentityEndpointMetadataTests</c>.
/// <para>
/// This is the half that <c>AuthorizationPolicyTests</c> cannot see. Those prove BudgetAccess
/// admits Owner/Accountant and denies everyone else; they say nothing about whether any
/// endpoint actually carries it. Delete the <c>.RequireAuthorization(BudgetAccess)</c> from
/// <c>BudgetingEndpoints.MapBudgetingEndpoints</c> and every policy test still passes while
/// <c>/api/budgeting</c> — the chart of accounts — opens to every authenticated account,
/// Driver included. These tests fail instead.
/// </para>
/// </summary>
public class BudgetingEndpointMetadataTests : IAsyncLifetime
{
    /// <summary>
    /// Endpoints deliberately reachable without authorization. Empty, and it must stay that
    /// way: nothing under /api/budgeting is public. An entry here is a decision, not a default.
    /// </summary>
    private static readonly HashSet<string> AnonymousByDesign = [];

    private WebApplication _app = null!;
    private List<RouteEndpoint> _endpoints = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();

        // The handler delegates take ISender / ITenantContext / ICurrentActor from DI; without
        // registrations the endpoint builder cannot classify those parameters (services vs.
        // body) and building the endpoints throws. Nothing is ever resolved — the app never runs.
        builder.Services.AddScoped<ISender, Sender>();
        builder.Services.AddScoped<ITenantContext, StubTenantContext>();
        builder.Services.AddScoped<ICurrentActor, StubCurrentActor>();

        _app = builder.Build();
        _app.MapBudgetingEndpoints();

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
    [InlineData("GET", "/api/budgeting/periods")]
    [InlineData("POST", "/api/budgeting/periods")]
    [InlineData("GET", "/api/budgeting/periods/{id:guid}")]
    [InlineData("POST", "/api/budgeting/periods/{id:guid}/finalize")]
    [InlineData("POST", "/api/budgeting/periods/{id:guid}/open")]
    [InlineData("POST", "/api/budgeting/periods/{id:guid}/begin-review")]
    [InlineData("POST", "/api/budgeting/periods/{id:guid}/close")]
    [InlineData("GET", "/api/budgeting/periods/{id:guid}/allocations")]
    [InlineData("PUT", "/api/budgeting/periods/{id:guid}/allocations/{codeId:guid}")]
    [InlineData("DELETE", "/api/budgeting/periods/{id:guid}/allocations/{codeId:guid}")]
    [InlineData("GET", "/api/budgeting/codes")]
    [InlineData("GET", "/api/budgeting/codes/owners")]
    [InlineData("POST", "/api/budgeting/codes")]
    [InlineData("PUT", "/api/budgeting/codes/{id:guid}")]
    [InlineData("POST", "/api/budgeting/codes/{id:guid}/activate")]
    [InlineData("POST", "/api/budgeting/codes/{id:guid}/deactivate")]
    [InlineData("DELETE", "/api/budgeting/codes/{id:guid}")]
    [InlineData("POST", "/api/budgeting/codes/starter-set")]
    public void Every_budgeting_endpoint_carries_the_BudgetAccess_policy(string method, string pattern)
    {
        var endpoint = Endpoint(method, pattern);

        var authorizeData = endpoint.Metadata.GetMetadata<IAuthorizeData>();

        Assert.NotNull(authorizeData);
        Assert.Equal(AuthorizationPolicies.BudgetAccess, authorizeData.Policy);
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
            "These /api/budgeting endpoints carry no authorization metadata: " +
            $"{string.Join(", ", unprotected)}. Add them to the group that carries " +
            "BudgetAccess, or allow-list them explicitly as anonymous by design.");
    }

    [Fact]
    public void The_pinned_route_list_covers_every_mapped_endpoint()
    {
        // Guards the guard: without this, an endpoint added to the group would be silently
        // absent from the Theory above (which asserts only the routes it names).
        Assert.Equal(18, _endpoints.Count);
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
