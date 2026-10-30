using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NorthernLink.Compliance.Infrastructure.Endpoints;
using NorthernLink.Compliance.Infrastructure.Retention;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using Xunit;

namespace NorthernLink.Compliance.Tests;

/// <summary>
/// Pins the module's auth surface without starting a server: the real <c>MapComplianceEndpoints</c>
/// is mapped onto a throwaway (never-run) WebApplication and the resulting endpoint metadata is
/// asserted directly — the <c>BudgetingEndpointMetadataTests</c> technique. <c>AuthorizationPolicyTests</c>
/// prove ComplianceAccess admits Owner/ComplianceOfficer and denies everyone else; this is the half
/// that proves every route actually carries it.
/// </summary>
public class ComplianceEndpointMetadataTests : IAsyncLifetime
{
    /// <summary>Nothing under /api/compliance is public. An entry here is a decision, not a default.</summary>
    private static readonly HashSet<string> AnonymousByDesign = [];

    private WebApplication _app = null!;
    private List<RouteEndpoint> _endpoints = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();

        // The handler delegates take ISender / ITenantContext / ICurrentActor / RetentionOptions
        // from DI; without registrations the endpoint builder cannot classify those parameters
        // (services vs. body) and building the endpoints throws. Nothing is ever resolved.
        builder.Services.AddScoped<ISender, Sender>();
        builder.Services.AddScoped<ITenantContext, StubTenantContext>();
        builder.Services.AddScoped<ICurrentActor, StubCurrentActor>();
        builder.Services.AddSingleton(new RetentionOptions());

        _app = builder.Build();
        _app.MapComplianceEndpoints();

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
    [InlineData("GET", "/api/compliance/rule-sets")]
    [InlineData("POST", "/api/compliance/rule-sets/publish")]
    [InlineData("POST", "/api/compliance/tenants/rule-set/apply")]
    [InlineData("GET", "/api/compliance/rules")]
    [InlineData("GET", "/api/compliance/rules/{key}")]
    [InlineData("GET", "/api/compliance/record-types")]
    [InlineData("GET", "/api/compliance/records")]
    [InlineData("POST", "/api/compliance/records")]
    [InlineData("GET", "/api/compliance/records/due-for-disposal")]
    [InlineData("GET", "/api/compliance/records/retention-status")]
    [InlineData("POST", "/api/compliance/records/dispose")]
    [InlineData("POST", "/api/compliance/records/disposal-review/sweep")]
    [InlineData("GET", "/api/compliance/records/{id:guid}")]
    [InlineData("GET", "/api/compliance/records/{id:guid}/print")]
    [InlineData("POST", "/api/compliance/records/{id:guid}/links")]
    [InlineData("POST", "/api/compliance/records/{id:guid}/anchors")]
    [InlineData("POST", "/api/compliance/records/{id:guid}/holds")]
    [InlineData("DELETE", "/api/compliance/records/{id:guid}/holds/{holdId:guid}")]
    [InlineData("POST", "/api/compliance/records/{id:guid}/supersede")]
    [InlineData("GET", "/api/compliance/destruction-log")]
    [InlineData("GET", "/api/compliance/regulatory-basis")]
    [InlineData("GET", "/api/compliance/regulatory-basis/by-keys")]
    [InlineData("GET", "/api/compliance/settings")]
    [InlineData("PUT", "/api/compliance/settings")]
    [InlineData("POST", "/api/compliance/seed-defaults")]
    public void Every_compliance_endpoint_carries_the_ComplianceAccess_policy(string method, string pattern)
    {
        var endpoint = Endpoint(method, pattern);

        var authorizeData = endpoint.Metadata.GetMetadata<IAuthorizeData>();

        Assert.NotNull(authorizeData);
        Assert.Equal(AuthorizationPolicies.ComplianceAccess, authorizeData.Policy);
    }

    [Fact]
    public void No_mapped_endpoint_is_left_without_authorization_metadata()
    {
        var unprotected = _endpoints
            .Where(e => e.Metadata.GetMetadata<IAuthorizeData>() is null)
            .Select(e => e.RoutePattern.RawText ?? "(no pattern)")
            .Where(pattern => !AnonymousByDesign.Contains(pattern))
            .ToList();

        Assert.True(
            unprotected.Count == 0,
            "These /api/compliance endpoints carry no authorization metadata: " +
            $"{string.Join(", ", unprotected)}. Add them to the group that carries ComplianceAccess.");
    }

    [Fact]
    public void The_pinned_route_list_covers_every_mapped_endpoint()
    {
        // Guards the guard: an endpoint added to the group would otherwise be silently absent
        // from the Theory above.
        Assert.Equal(25, _endpoints.Count);
    }

    [Fact]
    public void No_endpoint_carries_a_wider_policy()
    {
        // Compliance decisions are Owner + ComplianceOfficer only: a route that picked up
        // DispatchAccess or DriverAccess would open the registry to roles the plan never named.
        var wider = _endpoints
            .Select(e => (e.RoutePattern.RawText, e.Metadata.GetMetadata<IAuthorizeData>()?.Policy))
            .Where(pair => pair.Policy != AuthorizationPolicies.ComplianceAccess)
            .ToList();

        Assert.Empty(wider);
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
