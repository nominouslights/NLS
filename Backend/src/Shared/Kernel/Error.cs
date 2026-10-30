namespace NorthernLink.Shared.Kernel;

/// <summary>
/// Classifies an <see cref="Error"/> so outer layers (e.g. HTTP endpoints) can map it to a
/// transport-specific response (404/409/400/401/403) without inspecting error codes.
/// </summary>
public enum ErrorType
{
    None,
    NotFound,
    Conflict,
    Validation,
    Unauthorized,

    /// <summary>
    /// The caller is authenticated and may use the route, but not on this row — the
    /// caller-owns-this-record half of authorization failing (see <c>OwnRecordAccess</c>).
    /// Maps to 403, deliberately not 404 (the row exists) and not 401 (a 401 tells the
    /// frontends' transports to refresh the token and retry, which would loop).
    /// </summary>
    Forbidden,
}

/// <summary>
/// A coded error. Codes follow "Module.Subject.Problem", e.g. "Trips.Claim.AlreadyClaimed".
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.None)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
}
