using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NorthernLink.Compliance.Application.Records.QueueForDisposalReview;
using NorthernLink.Compliance.Infrastructure.Endpoints;
using NorthernLink.Compliance.Infrastructure.Retention;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using Xunit;

namespace NorthernLink.Compliance.Tests;

/// <summary>
/// <c>POST records/disposal-review/sweep?asOf=</c> may only look back. Review status is what
/// disposal trusts, so a future <c>asOf</c> would make records disposable before their retention
/// ends. Drives the real mapped endpoint delegate (no server) with a recording sender.
/// </summary>
public class ComplianceSweepEndpointTests : IAsyncLifetime
{
    private readonly RecordingSender _sender = new();
    private WebApplication _app = null!;
    private RequestDelegate _sweep = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<ISender>(_sender);
        builder.Services.AddSingleton<ITenantContext>(new FixedTenant());
        builder.Services.AddSingleton<ICurrentActor>(new NoActor());
        builder.Services.AddSingleton(new RetentionOptions());

        _app = builder.Build();
        _app.MapComplianceEndpoints();

        _sweep = ((IEndpointRouteBuilder)_app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "/api/compliance/records/disposal-review/sweep")
            .RequestDelegate!;

        await Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    private async Task<HttpContext> PostAsync(string queryString)
    {
        var context = new DefaultHttpContext { RequestServices = _app.Services };
        context.Request.Method = HttpMethods.Post;
        context.Request.QueryString = new QueryString(queryString);
        context.Response.Body = new MemoryStream();
        await _sweep(context);
        context.Response.Body.Position = 0;
        return context;
    }

    [Fact]
    public async Task A_future_as_of_date_is_refused_and_nothing_is_swept()
    {
        var future = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        var context = await PostAsync($"?asOf={future:yyyy-MM-dd}");

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("Compliance.Record.SweepDateInFuture", body.RootElement.GetProperty("code").GetString());
        Assert.NotEqual(0, body.RootElement.GetProperty("basis").GetArrayLength());
        Assert.Empty(_sender.Sweeps);
    }

    [Fact]
    public async Task A_past_as_of_date_and_no_date_both_sweep()
    {
        var past = new DateOnly(2026, 1, 31);

        var withDate = await PostAsync($"?asOf={past:yyyy-MM-dd}");
        var withoutDate = await PostAsync(string.Empty);

        Assert.Equal(StatusCodes.Status200OK, withDate.Response.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, withoutDate.Response.StatusCode);
        Assert.Equal(past, _sender.Sweeps[0].AsOf);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), _sender.Sweeps[1].AsOf);
    }

    private sealed class RecordingSender : ISender
    {
        public List<QueueForDisposalReviewCommand> Sweeps { get; } = [];

        public Task<Result> Send(ICommand command, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
        {
            Sweeps.Add((QueueForDisposalReviewCommand)(object)command);
            return Task.FromResult(Result.Success((TResponse)(object)0));
        }

        public Task<Result<TResponse>> Query<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedTenant : ITenantContext
    {
        public Guid? TenantId => TestCompliance.TenantId;

        public TenantType? TenantType => null;
    }

    private sealed class NoActor : ICurrentActor
    {
        public Guid? UserId => null;

        public string? Email => null;

        public IReadOnlyCollection<string> Roles => [];
    }
}
