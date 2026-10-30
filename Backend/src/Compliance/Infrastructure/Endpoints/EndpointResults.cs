using Microsoft.AspNetCore.Http;
using NorthernLink.Compliance.Domain;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Infrastructure.Endpoints;

/// <summary>
/// Maps a failed <see cref="Result"/> to HTTP via <see cref="ErrorType"/>:
/// NotFound→404, Conflict→409, Validation→400, Unauthorized→401, Forbidden→403. Error bodies are
/// <c>{ code, message, basis }</c> — the platform's <c>{ code, message }</c> shape plus this
/// module's addition: <c>basis</c> is the regulatory-basis keys the error was defined with
/// (<see cref="ComplianceError"/>), so a block explains itself and a screen can fetch the rule's
/// text from <c>GET /api/compliance/regulatory-basis/by-keys</c>. Empty for an error this module
/// did not define. (Module-local copy — each library carries its own so it stays extractable.)
/// </summary>
internal static class EndpointResults
{
    public static IResult Problem(Error error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(Body(error)),
        ErrorType.Conflict => Results.Conflict(Body(error)),
        ErrorType.Validation => Results.BadRequest(Body(error)),
        ErrorType.Unauthorized => Results.Unauthorized(),
        ErrorType.Forbidden => Results.Json(Body(error), statusCode: StatusCodes.Status403Forbidden),
        _ => Results.BadRequest(Body(error)),
    };

    private static ErrorBody Body(Error error) => new(error.Code, error.Message, ComplianceError.BasisKeysFor(error.Code));

    private sealed record ErrorBody(string Code, string Message, IReadOnlyList<string> Basis);
}
