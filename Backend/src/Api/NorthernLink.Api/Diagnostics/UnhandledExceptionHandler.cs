using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace NorthernLink.Api.Diagnostics;

/// <summary>
/// The one place an unhandled exception becomes a log line and a response.
///
/// Before this existed the API had no exception handling at all: an unhandled exception got the
/// framework's bare 500 with an empty body, and the stack trace landed in the log stream at
/// Information level alongside every EF Core SQL statement — so the one line that mattered was
/// buried under thousands that did not. Diagnosing a production 500 meant guessing.
///
/// Two rules here, both deliberate:
///
/// 1. EVERYTHING is logged server-side, in full, including the stack. A Postgres error also gets
///    its <c>SqlState</c> pulled out and put in the message, because that five-character code is
///    the entire diagnosis — <c>42703</c> is a missing column (the schema is behind the code, see
///    the `migrations` skill), <c>42P01</c> a missing table, <c>23505</c> a unique violation. It
///    is otherwise buried several frames into the exception's ToString().
///
/// 2. The RESPONSE says almost nothing. It carries the status, a generic title and the traceId —
///    never the message, never the stack. The API is reachable from the public internet through
///    each frontend's proxy, and an exception message leaks table names, column names and query
///    shapes. The traceId is the correlation handle: find it in the response, grep it in the log.
///    The exception detail is added ONLY in Development, where the browser and the log are the
///    same machine and nothing is exposed.
/// </summary>
public sealed class UnhandledExceptionHandler(
    ILogger<UnhandledExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;

        // The Postgres SqlState, when there is one, is the fastest route to a cause — so it goes
        // in the message rather than only inside the exception's own rendering.
        var postgresCode = FindPostgresErrorCode(exception);

        logger.LogError(
            exception,
            "Unhandled exception on {Method} {Path}{Query} — {ExceptionType}{PostgresCode}. traceId={TraceId}",
            httpContext.Request.Method,
            httpContext.Request.Path.Value,
            httpContext.Request.QueryString.HasValue ? httpContext.Request.QueryString.Value : string.Empty,
            exception.GetType().Name,
            postgresCode is null ? string.Empty : $" (Postgres {postgresCode})",
            traceId);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            // Deliberately vague in the body; the traceId is how a reader finds the real line.
            Detail = $"The request could not be completed. Quote traceId {traceId} when reporting this.",
            Instance = httpContext.Request.Path,
        };

        problem.Extensions["traceId"] = traceId;

        if (environment.IsDevelopment())
        {
            // Local only: the browser and the log are the same machine, so there is nothing to leak.
            problem.Extensions["exception"] = exception.GetType().FullName;
            problem.Extensions["message"] = exception.Message;

            if (postgresCode is not null)
            {
                problem.Extensions["postgresCode"] = postgresCode;
            }
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }

    /// <summary>
    /// Walks the inner-exception chain for a <see cref="PostgresException"/>. EF Core wraps the
    /// driver's exception, so the code is never on the outermost one.
    /// </summary>
    private static string? FindPostgresErrorCode(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
            {
                return postgres.SqlState;
            }
        }

        return null;
    }
}
