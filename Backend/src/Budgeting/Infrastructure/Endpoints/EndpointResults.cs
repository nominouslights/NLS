using Microsoft.AspNetCore.Http;
using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Domain.Qbo;

namespace NorthernLink.Budgeting.Infrastructure.Endpoints;

/// <summary>
/// Maps a failed <see cref="Result"/> to HTTP via <see cref="ErrorType"/>:
/// NotFound→404, Conflict→409, Validation→400, Unauthorized→401. Error bodies are
/// always <c>{ code, message }</c> — the shape frontends parse. (Module-local copy —
/// each library carries its own so it stays extractable.)
/// <para>
/// Two QuickBooks errors are matched by code first, because <see cref="ErrorType"/> has no member
/// for "the upstream failed": <see cref="QboConnectionErrors.UpstreamCodes"/> → 502 (Intuit
/// refused or could not be reached) and <see cref="QboConnectionErrors.NotConfigured"/> → 503 (this
/// server lacks its Intuit credentials). Both keep the same <c>{ code, message }</c> body.
/// </para>
/// </summary>
internal static class EndpointResults
{
    public static IResult Problem(Error error)
    {
        if (QboConnectionErrors.UpstreamCodes.Contains(error.Code))
        {
            return Results.Json(Body(error), statusCode: StatusCodes.Status502BadGateway);
        }

        if (error.Code == QboConnectionErrors.NotConfigured.Code)
        {
            return Results.Json(Body(error), statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return ByType(error);
    }

    private static IResult ByType(Error error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(Body(error)),
        ErrorType.Conflict => Results.Conflict(Body(error)),
        ErrorType.Validation => Results.BadRequest(Body(error)),
        ErrorType.Unauthorized => Results.Unauthorized(),
        _ => Results.BadRequest(Body(error)),
    };

    private static ErrorBody Body(Error error) => new(error.Code, error.Message);

    private sealed record ErrorBody(string Code, string Message);
}
