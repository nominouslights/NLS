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
/// Pins the deadhead-conversion surface without starting a server (the
/// TripChangeRouteEndpointMetadataTests technique). Converting deletes manifests and changes what
/// gates a run, so both routes must carry DispatchAccess — never the wider DriverAccess the
/// board's read routes use on the same "/api/trips" prefix. Also pins that PUT /api/trips/{id}
/// can no longer carry the flag.
/// </summary>
public class TripDeadheadConversionEndpointMetadataTests : IAsyncLifetime
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
            .Where(e => e.RoutePattern.RawText?.Contains("/convert-to-", StringComparison.Ordinal) == true)
            .ToList();

        await Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Theory]
    [InlineData("POST", "/api/trips/{id:guid}/convert-to-deadhead")]
    [InlineData("POST", "/api/trips/{id:guid}/convert-to-passenger-trip")]
    public void Conversion_endpoints_carry_DispatchAccess_only(string method, string pattern)
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
    public void The_pinned_route_list_covers_every_conversion_endpoint()
    {
        Assert.Equal(2, _endpoints.Count);
    }

    [Fact]
    public void The_edit_body_has_no_deadhead_flag_and_a_stale_client_sending_one_still_binds()
    {
        Assert.Null(typeof(UpdateTripRequest).GetProperty("IsEmptyLeg"));

        // Minimal APIs bind with the web defaults (Program.cs only adds the enum converter):
        // unknown members are skipped, so an old Dispatcher build sending isEmptyLeg gets its
        // other edits applied and the flag ignored, rather than a 400.
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        var request = System.Text.Json.JsonSerializer.Deserialize<UpdateTripRequest>(
            """
            {"serviceDate":"2026-07-21","windowStart":"06:30:00","serviceType":"ContractCrew","routeName":"Thompson ↔ Lynn Lake",
             "origin":"Thompson","destination":"Lynn Lake","distanceKm":320,"isEmptyLeg":true,"poNumber":"PO-9"}
            """,
            options);

        Assert.NotNull(request);
        Assert.Equal("PO-9", request.PoNumber);
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
