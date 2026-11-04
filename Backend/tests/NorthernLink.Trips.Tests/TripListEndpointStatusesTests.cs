using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Trips;
using NorthernLink.Trips.Application.Trips.GetTrips;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Infrastructure.Endpoints;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// GET /api/trips's <c>?statuses=</c> through the REAL minimal-API binding: the mapped
/// endpoint's request delegate is invoked over a <see cref="DefaultHttpContext"/> (no server,
/// no TestHost package), with the real <see cref="GetTripsQueryHandler"/> in front of a
/// recording read side. This is what pins the wire contract a unit test of
/// <see cref="TripStatusFilter"/> cannot — that <c>?statuses=</c> with no value reaches the
/// parser as an empty string (and so is rejected) rather than binding to null and silently
/// meaning "every status", and that errors come back as 400 <c>{ code, message }</c>.
/// </summary>
public class TripListEndpointStatusesTests : IAsyncLifetime
{
    private static readonly Guid ClientId = Guid.Parse("5a7e0000-0000-0000-0000-00000000acc1");

    private readonly FakeTripReadService _reads = new();
    private WebApplication _app = null!;
    private RouteEndpoint _list = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddScoped<ISender, Sender>();
        builder.Services.AddSingleton<ITenantContext, StubTenantContext>();
        builder.Services.AddSingleton<ICurrentActor>(FakeCurrentActor.Dispatcher);
        builder.Services.AddSingleton<IDriverLookupRepository>(new FakeDriverLookupRepository());
        builder.Services.AddScoped<TripOperatorAccess>();
        builder.Services.AddSingleton<ITripReadService>(_reads);
        builder.Services.AddScoped<IQueryHandler<GetTripsQuery, IReadOnlyList<TripResponse>>, GetTripsQueryHandler>();

        _app = builder.Build();
        _app.MapTripsEndpoints();

        _list = ((IEndpointRouteBuilder)_app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText?.TrimEnd('/') == "/api/trips"
                && e.Metadata.GetMetadata<HttpMethodMetadata>() is { } methods
                && methods.HttpMethods.Contains("GET"));

        await Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    private async Task<(int Status, JsonElement Body)> GetAsync(string queryString)
    {
        await using var scope = _app.Services.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/trips";
        context.Request.QueryString = new QueryString(queryString);
        using var body = new MemoryStream();
        context.Response.Body = body;

        await _list.RequestDelegate!(context);

        using var document = JsonDocument.Parse(body.ToArray());
        return (context.Response.StatusCode, document.RootElement.Clone());
    }

    private static void AssertProblem((int Status, JsonElement Body) response, Error expected)
    {
        Assert.Equal(StatusCodes.Status400BadRequest, response.Status);
        Assert.Equal(expected.Code, response.Body.GetProperty("code").GetString());
        Assert.Equal(expected.Message, response.Body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task The_accruals_read_is_one_call_with_the_whole_set_to_only_and_the_client()
    {
        var response = await GetAsync(
            $"?to=2026-10-31&clientId={ClientId}&statuses=Scheduled,InProgress,ReadyForBilling,Invoiced");

        Assert.Equal(StatusCodes.Status200OK, response.Status);
        var filter = Assert.IsType<TripFilter>(_reads.LastFilter);
        Assert.Equal(
            [TripStatus.Scheduled, TripStatus.InProgress, TripStatus.ReadyForBilling, TripStatus.Invoiced],
            filter.Statuses);
        Assert.Null(filter.Status);
        Assert.Null(filter.From);
        Assert.Equal(new DateOnly(2026, 10, 31), filter.To);
        Assert.Equal(ClientId, filter.ClientId);
        // Unpaged — the full match, in the usual envelope.
        Assert.Null(filter.Page);
        Assert.Null(filter.PageSize);
        Assert.Equal(JsonValueKind.Array, response.Body.GetProperty("items").ValueKind);
    }

    [Fact]
    public async Task A_single_status_still_reaches_the_read_side_as_Status_with_no_set()
    {
        var response = await GetAsync("?status=Scheduled&date=2026-10-31");

        Assert.Equal(StatusCodes.Status200OK, response.Status);
        Assert.Equal(TripStatus.Scheduled, _reads.LastFilter!.Status);
        Assert.Null(_reads.LastFilter.Statuses);
    }

    [Fact]
    public async Task No_status_parameter_at_all_is_still_no_narrowing()
    {
        var response = await GetAsync("?date=2026-10-31");

        Assert.Equal(StatusCodes.Status200OK, response.Status);
        Assert.Null(_reads.LastFilter!.Status);
        Assert.Null(_reads.LastFilter.Statuses);
    }

    [Fact]
    public async Task Repeating_the_parameter_binds_the_same_as_the_comma_list()
    {
        // A string parameter given several values binds them comma-joined, so a client that
        // builds the query with URLSearchParams.append gets exactly the comma-list behaviour.
        var response = await GetAsync("?statuses=Scheduled&statuses=InProgress");

        Assert.Equal(StatusCodes.Status200OK, response.Status);
        Assert.Equal([TripStatus.Scheduled, TripStatus.InProgress], _reads.LastFilter!.Statuses);
    }

    [Theory]
    [InlineData("?status=Bogus")]
    [InlineData("?status=2")]
    public async Task An_unknown_single_status_is_still_400_InvalidStatusFilter(string query)
    {
        AssertProblem(await GetAsync(query), TripErrors.InvalidStatusFilter);
        Assert.Null(_reads.LastFilter);
    }

    [Theory]
    [InlineData("?statuses=Scheduled,Bogus")]
    [InlineData("?statuses=scheduled")]
    [InlineData("?statuses=Scheduled,2")]
    public async Task An_unknown_name_in_the_set_is_400_InvalidStatusFilter(string query)
    {
        AssertProblem(await GetAsync(query), TripErrors.InvalidStatusFilter);
        Assert.Null(_reads.LastFilter);
    }

    [Theory]
    [InlineData("?statuses=")]
    [InlineData("?statuses=%20")]
    [InlineData("?statuses=,")]
    public async Task An_empty_set_is_400_EmptyStatusesFilter_not_every_status(string query)
    {
        AssertProblem(await GetAsync(query), TripErrors.EmptyStatusesFilter);
        Assert.Null(_reads.LastFilter);
    }

    [Fact]
    public async Task Both_filters_together_is_400_ConflictingStatusFilters()
    {
        AssertProblem(await GetAsync("?status=Scheduled&statuses=InProgress"), TripErrors.ConflictingStatusFilters);
        Assert.Null(_reads.LastFilter);
    }

    private sealed class StubTenantContext : ITenantContext
    {
        public Guid? TenantId => TestPlanning.TenantId;

        public TenantType? TenantType => Shared.Kernel.TenantType.Internal;
    }
}
