namespace NorthernLink.SharedKernel;

/// <summary>
/// A coded error. Codes follow "Module.Subject.Problem", e.g. "Trips.Claim.AlreadyClaimed".
/// </summary>
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error NotFound(string code, string message) => new(code, message);
    public static Error Conflict(string code, string message) => new(code, message);
    public static Error Validation(string code, string message) => new(code, message);
    public static Error Unauthorized(string code, string message) => new(code, message);
}
