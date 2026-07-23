using Microsoft.AspNetCore.Http;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Billing.Infrastructure.Endpoints;

/// <summary>
/// Maps a failed <see cref="Result"/> to HTTP via <see cref="ErrorType"/>:
/// NotFound→404, Conflict→409, Validation→400, Unauthorized→401. Error bodies are
/// always <c>{ code, message }</c> — the shape frontends parse. (Module-local copy —
/// each library carries its own so it stays extractable with Shared alone.)
/// </summary>
internal static class EndpointResults
{
    public static IResult Problem(Error error) => error.Type switch
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
