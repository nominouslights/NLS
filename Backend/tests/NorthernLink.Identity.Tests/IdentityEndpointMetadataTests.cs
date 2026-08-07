using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NorthernLink.Identity.Infrastructure.Endpoints;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using Xunit;

namespace NorthernLink.Identity.Tests;

/// <summary>
/// Pins the module's auth surface without starting a server: the real
/// <c>MapIdentityEndpoints</c> is mapped onto a throwaway (never-run) WebApplication and
/// the resulting endpoint metadata is asserted directly. Minting a bootstrap token must
/// require the AdminOnly policy; everything else on this module is anonymous by design —
/// it runs before a caller has a token.
/// </summary>
public class IdentityEndpointMetadataTests : IAsyncLifetime
{
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
        _app.MapIdentityEndpoints();

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

    [Fact]
    public void Bootstrap_token_minting_requires_the_AdminOnly_policy()
    {
        var endpoint = Endpoint("POST", "/api/identity/admin/bootstrap-token");

        var authorizeData = endpoint.Metadata.GetMetadata<IAuthorizeData>();

        Assert.NotNull(authorizeData);
        Assert.Equal("AdminOnly", authorizeData.Policy);
    }

    [Fact]
    public void Me_requires_authentication_but_no_particular_role()
    {
        // Any signed-in caller, whatever their role — a client that is about to be told "you may
        // not be here" still has to be able to read the role that decided it.
        var endpoint = Endpoint("GET", "/api/identity/auth/me");

        var authorizeData = endpoint.Metadata.GetMetadata<IAuthorizeData>();

        Assert.NotNull(authorizeData);
        Assert.Null(authorizeData.Policy);
        Assert.Null(authorizeData.Roles);
    }

    [Theory]
    [InlineData("POST", "/api/identity/auth/login")]
    [InlineData("POST", "/api/identity/auth/refresh")]
    [InlineData("POST", "/api/identity/auth/logout")]
    [InlineData("GET", "/api/identity/auth/setup-status")]
    [InlineData("POST", "/api/identity/auth/setup")]
    [InlineData("POST", "/api/identity/admin/bootstrap")]
    public void Anonymous_by_design_endpoints_carry_no_authorization_metadata(string method, string pattern)
    {
        var endpoint = Endpoint(method, pattern);

        Assert.Null(endpoint.Metadata.GetMetadata<IAuthorizeData>());
    }

    private sealed class StubTenantContext : ITenantContext
    {
        public Guid? TenantId => null;

        public TenantType? TenantType => null;
    }
}
