using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Infrastructure.Endpoints;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// Pins the route-change surface without starting a server (the BookeoImportEndpointMetadataTests
/// technique). Re-routing rewrites both legs of a round trip and can strand passengers, so both
/// routes must carry DispatchAccess — never the wider DriverAccess the board's read routes use
/// on the same "/api/trips" prefix.
/// </summary>
public class TripChangeRouteEndpointMetadataTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private List<RouteEndpoint> _endpoints = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddScoped<ISender, Sender>();
        builder.Services.AddScoped<ITenantContext, StubTenantContext>();
        builder.Services.AddScoped<ICurrentActor, StubCurrentActor>();

        _app = builder.Build();
        _app.MapTripsEndpoints();

        _endpoints = ((IEndpointRouteBuilder)_app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.Contains("/change-route", StringComparison.Ordinal) == true)
            .ToList();

        await Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Theory]
    [InlineData("GET", "/api/trips/{id:guid}/change-route/preview")]
    [InlineData("POST", "/api/trips/{id:guid}/change-route")]
    public void Route_change_endpoints_carry_DispatchAccess_only(string method, string pattern)
    {
        var endpoint = _endpoints.SingleOrDefault(e =>
            e.RoutePattern.RawText == pattern
            && e.Metadata.GetMetadata<HttpMethodMetadata>() is { } methods
            && methods.HttpMethods.Contains(method));
        Assert.True(endpoint is not null, $"Expected a mapped endpoint {method} {pattern}.");

        var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy).ToList();
        Assert.Equal([AuthorizationPolicies.DispatchAccess], policies);
    }

    [Fact]
    public void The_pinned_route_list_covers_every_route_change_endpoint()
    {
        Assert.Equal(2, _endpoints.Count);
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
