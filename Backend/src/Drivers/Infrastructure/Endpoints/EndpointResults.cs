using Microsoft.AspNetCore.Http;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Infrastructure.Endpoints;

/// <summary>
/// Maps a failed <see cref="Result"/> to HTTP via <see cref="ErrorType"/>:
/// NotFound→404, Conflict→409, Validation→400, Unauthorized→401, Forbidden→403. Error bodies
/// are always <c>{ code, message }</c> — the shape frontends parse. Module-local copy:
/// domain libraries never reference each other, and this is endpoint plumbing, not
/// shared kernel.
/// </summary>
internal static class EndpointResults
{
    public static IResult Problem(Error error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(Body(error)),
        ErrorType.Conflict => Results.Conflict(Body(error)),
        ErrorType.Validation => Results.BadRequest(Body(error)),
        ErrorType.Unauthorized => Results.Unauthorized(),
        // Results.Json with the status, not Results.Forbid(): that one runs the authentication
        // handler's forbid flow and drops the { code, message } the frontends read.
        ErrorType.Forbidden => Results.Json(Body(error), statusCode: StatusCodes.Status403Forbidden),
        _ => Results.BadRequest(Body(error)),
    };

    private static ErrorBody Body(Error error) => new(error.Code, error.Message);

    private sealed record ErrorBody(string Code, string Message);
}
