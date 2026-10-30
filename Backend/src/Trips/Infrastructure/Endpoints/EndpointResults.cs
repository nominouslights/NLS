using Microsoft.AspNetCore.Http;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Infrastructure.Endpoints;

/// <summary>
/// Maps a failed <see cref="Result"/> to HTTP via <see cref="ErrorType"/>:
/// NotFound→404, Conflict→409, Validation→400, Unauthorized→401, Forbidden→403. Error
/// bodies are always <c>{ code, message }</c> — the shape frontends parse. (Deliberately
/// duplicated per library — a shared helper would be a cross-library coupling for 20 lines.)
/// </summary>
internal static class EndpointResults
{
    public static IResult Problem(Error error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(Body(error)),
        ErrorType.Conflict => Results.Conflict(Body(error)),
        ErrorType.Validation => Results.BadRequest(Body(error)),
        ErrorType.Unauthorized => Results.Unauthorized(),
        // The caller-owns-this-trip check failing (Trips.Trip.NotYourTrip). 403 with a body,
        // not Results.Forbid(): that one runs the authentication handler's forbid flow and
        // drops the { code, message } the Driver Field App reads.
        ErrorType.Forbidden => Results.Json(Body(error), statusCode: StatusCodes.Status403Forbidden),
        _ => Results.BadRequest(Body(error)),
    };

    private static ErrorBody Body(Error error) => new(error.Code, error.Message);

    private sealed record ErrorBody(string Code, string Message);
}
