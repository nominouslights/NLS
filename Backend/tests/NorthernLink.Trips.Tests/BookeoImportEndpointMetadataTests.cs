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
/// Pins the Bookeo import's auth surface without starting a server (the
/// <c>BudgetingEndpointMetadataTests</c> technique): the real <c>MapTripsEndpoints</c> is mapped
/// onto a never-run WebApplication and the endpoint metadata asserted. Importing writes trips and
/// manifests, so every route must carry DispatchAccess — never DriverAccess, never a bare
/// authorize — and the routes must be exactly the ones the Dispatcher's modal is built against.
/// </summary>
public class BookeoImportEndpointMetadataTests : IAsyncLifetime
{
    private const string Prefix = "/api/trips/imports/bookeo";

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
            .Where(e => e.RoutePattern.RawText?.StartsWith(Prefix, StringComparison.Ordinal) == true)
            .ToList();

        await Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Theory]
    [InlineData("POST", Prefix + "/preview")]
    [InlineData("POST", Prefix + "/{batchId:guid}/commit")]
    [InlineData("GET", Prefix + "/product-mappings")]
    [InlineData("PUT", Prefix + "/product-mappings")]
    [InlineData("DELETE", Prefix + "/product-mappings/{id:guid}")]
    [InlineData("GET", Prefix + "/unit-mappings")]
    [InlineData("PUT", Prefix + "/unit-mappings")]
    [InlineData("DELETE", Prefix + "/unit-mappings/{id:guid}")]
    [InlineData("GET", Prefix + "/batches")]
    public void Every_bookeo_import_endpoint_carries_DispatchAccess(string method, string pattern)
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
    public void The_pinned_route_list_covers_every_mapped_import_endpoint()
    {
        Assert.Equal(9, _endpoints.Count);
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
