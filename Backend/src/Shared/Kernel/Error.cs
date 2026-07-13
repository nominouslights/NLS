namespace NorthernLink.Shared.Kernel;

/// <summary>
/// Classifies an <see cref="Error"/> so outer layers (e.g. HTTP endpoints) can map it to a
/// transport-specific response (404/409/400/401) without inspecting error codes.
/// </summary>
public enum ErrorType
{
    None,
    NotFound,
    Conflict,
    Validation,
    Unauthorized,
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
}
