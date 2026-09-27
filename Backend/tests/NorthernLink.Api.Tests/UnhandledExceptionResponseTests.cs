using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Api.Diagnostics;
using Npgsql;
using Xunit;

namespace NorthernLink.Api.Tests;

/// <summary>
/// What an unhandled exception is allowed to tell the caller.
///
/// <para>
/// The API is reachable from the public internet through each frontend's <c>/api/*</c> proxy, so a
/// 500 body is attacker-readable. An exception message from this stack names tables, columns and
/// query shapes — <c>42703: column d.booking_id does not exist</c> hands over a piece of the
/// schema. These tests pin the split the handler exists to enforce: <b>everything to the log,
/// almost nothing to the response</b>, with a traceId as the only bridge between the two.
/// </para>
///
/// <para>
/// <b>Not covered here:</b> that <c>Program.cs</c> actually calls <c>app.UseExceptionHandler()</c>.
/// These exercise the handler directly, because asserting the middleware pipeline needs
/// <c>Microsoft.AspNetCore.TestHost</c>, and adding a package to the shared
/// <c>Directory.Packages.props</c> for it is a platform change rather than part of this fix.
/// Deleting that one line in <c>Program.cs</c> would leave these green — so it carries a comment
/// saying why it is there.
/// </para>
/// </summary>
public sealed class UnhandledExceptionResponseTests
{
    private const string Secret = "column d.booking_id does not exist";

    private static readonly Exception WrappedPostgres = new InvalidOperationException(
        "An exception occurred while reading from the store.",
        new PostgresException(Secret, "ERROR", "ERROR", "42703"));

    /// <summary>Runs the handler over a request shaped like the one that failed in production.</summary>
    private static async Task<(string Body, int Status)> HandleAsync(string environmentName, Exception thrown)
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-0HN7ABCDEF",
        };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/notifications/emails";
        context.Request.QueryString = new QueryString("?tripId=30269d57-266c-4e27-97fc-6e698b2a1382");

        using var body = new MemoryStream();
        context.Response.Body = body;

        var handler = new UnhandledExceptionHandler(
            NullLogger<UnhandledExceptionHandler>.Instance,
            new StubEnvironment(environmentName));

        var handled = await handler.TryHandleAsync(context, thrown, CancellationToken.None);
        Assert.True(handled, "The handler must claim the exception; returning false re-throws it.");

        return (System.Text.Encoding.UTF8.GetString(body.ToArray()), context.Response.StatusCode);
    }

    [Fact]
    public async Task Production_answers_500_without_leaking_the_exception_message()
    {
        var (body, status) = await HandleAsync(Environments.Production, new InvalidOperationException(Secret));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);

        // The whole point. If this fails, a 500 is handing out the schema.
        Assert.DoesNotContain(Secret, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at NorthernLink", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Production_still_returns_a_traceId_so_the_log_line_can_be_found()
    {
        var (body, _) = await HandleAsync(Environments.Production, new InvalidOperationException(Secret));

        var problem = JsonSerializer.Deserialize<JsonElement>(body);

        Assert.Equal("trace-0HN7ABCDEF", problem.GetProperty("traceId").GetString());

        // Without the id in the human-readable line the body is a dead end: nothing connects the
        // caller's 500 to the server log entry that explains it.
        Assert.Contains("trace-0HN7ABCDEF", problem.GetProperty("detail").GetString()!);
    }

    [Fact]
    public async Task A_postgres_sqlstate_never_reaches_the_production_body()
    {
        var (body, _) = await HandleAsync(Environments.Production, WrappedPostgres);

        Assert.DoesNotContain("42703", body);
        Assert.DoesNotContain("booking_id", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Development_includes_the_detail_because_the_log_and_the_browser_are_one_machine()
    {
        var (body, _) = await HandleAsync(Environments.Development, new InvalidOperationException(Secret));

        var problem = JsonSerializer.Deserialize<JsonElement>(body);

        Assert.Equal(Secret, problem.GetProperty("message").GetString());
        Assert.Contains("InvalidOperationException", problem.GetProperty("exception").GetString()!);
    }

    [Fact]
    public async Task Development_surfaces_a_wrapped_postgres_sqlstate()
    {
        // 42703 is undefined_column — the schema lagging the code, which is the failure this
        // handler was written during. EF Core wraps the driver's exception, so the code is never
        // on the outermost one; finding it is the difference between a diagnosis and a guess.
        var (body, _) = await HandleAsync(Environments.Development, WrappedPostgres);

        var problem = JsonSerializer.Deserialize<JsonElement>(body);

        Assert.Equal("42703", problem.GetProperty("postgresCode").GetString());
    }

    private sealed class StubEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "NorthernLink.Api.Tests";

        public string ContentRootPath { get; set; } = string.Empty;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
