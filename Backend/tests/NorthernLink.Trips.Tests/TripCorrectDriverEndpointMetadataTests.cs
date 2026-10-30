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
/// Pins the driver-correction surface without starting a server (the
/// TripDeadheadConversionEndpointMetadataTests technique). Rewriting who drove a finished run is
/// dispatch work, so the route carries DispatchAccess only — never the wider DriverAccess the
/// board's read routes use on the same "/api/trips" prefix. Also pins that the body has no
/// "who" member: the actor comes from the token.
/// </summary>
public class TripCorrectDriverEndpointMetadataTests : IAsyncLifetime
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
            .Where(e => e.RoutePattern.RawText?.Contains("/correct-driver", StringComparison.Ordinal) == true)
            .ToList();

        await Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public void Correct_driver_is_a_POST_carrying_DispatchAccess_only()
    {
        var endpoint = Assert.Single(_endpoints);
        Assert.Equal("/api/trips/{id:guid}/correct-driver", endpoint.RoutePattern.RawText);
        Assert.Equal(["POST"], endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);

        var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy).ToList();
        Assert.Equal([AuthorizationPolicies.DispatchAccess], policies);
    }

    [Fact]
    public void The_body_is_driverId_and_reason_and_nothing_names_the_actor()
    {
        var members = typeof(CorrectTripDriverRequest).GetProperties().Select(p => p.Name).ToList();
        Assert.Equal(["DriverId", "Reason"], members);

        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var request = System.Text.Json.JsonSerializer.Deserialize<CorrectTripDriverRequest>(
            """{"driverId":"00000000-0000-0000-0000-0000000000d2","reason":"wrong driver","enteredBy":"forged@x.ca"}""",
            options);

        Assert.NotNull(request);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-0000000000d2"), request.DriverId);
        Assert.Equal("wrong driver", request.Reason);
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
