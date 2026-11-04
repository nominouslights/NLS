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
/// Pins POST /api/trips/{id}/reopen without starting a server (the
/// TripDeadheadConversionEndpointMetadataTests technique). Reopening undoes a lifecycle step, so
/// it carries exactly the policy close-without-billing does — DispatchAccess — and never the wider
/// DriverAccess the board's read and status routes use on the same "/api/trips" prefix.
/// </summary>
public class TripReopenEndpointMetadataTests : IAsyncLifetime
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
            .ToList();

        await Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    private List<string?> PoliciesOf(string method, string pattern)
    {
        var endpoint = _endpoints.SingleOrDefault(e =>
            e.RoutePattern.RawText == pattern
            && e.Metadata.GetMetadata<HttpMethodMetadata>() is { } methods
            && methods.HttpMethods.Contains(method));
        Assert.True(endpoint is not null, $"Expected a mapped endpoint {method} {pattern}.");
        return endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy).ToList();
    }

    [Fact]
    public void Reopen_carries_DispatchAccess_only()
    {
        Assert.Equal([AuthorizationPolicies.DispatchAccess], PoliciesOf("POST", "/api/trips/{id:guid}/reopen"));
    }

    [Fact]
    public void Reopen_carries_the_same_policy_as_close_without_billing()
    {
        Assert.Equal(
            PoliciesOf("POST", "/api/trips/{id:guid}/close-without-billing"),
            PoliciesOf("POST", "/api/trips/{id:guid}/reopen"));
    }

    [Fact]
    public void The_body_binds_the_reason_from_camel_case_json()
    {
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var request = System.Text.Json.JsonSerializer.Deserialize<ReopenTripRequest>(
            """{"reason":"Cancelled the wrong run"}""", options);

        Assert.NotNull(request);
        Assert.Equal("Cancelled the wrong run", request.Reason);
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
