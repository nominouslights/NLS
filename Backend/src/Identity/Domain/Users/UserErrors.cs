using NorthernLink.Shared.Kernel;

namespace NorthernLink.Identity.Domain.Users;

/// <summary>All domain errors the User aggregate (and its handlers) can produce.</summary>
public static class UserErrors
{
    public static readonly Error InvalidEmail = Error.Validation(
        "Identity.User.InvalidEmail", "A valid email address is required.");

    public static readonly Error InvalidPasswordHash = Error.Validation(
        "Identity.User.InvalidPasswordHash", "A password hash is required.");

    public static readonly Error InvalidPassword = Error.Validation(
        "Identity.User.InvalidPassword", "A password is required.");

    public static readonly Error InvalidRole = Error.Validation(
        "Identity.User.InvalidRole",
        $"A role is required, and must be one of: {string.Join(", ", Roles.Internal)}.");

    public static readonly Error DuplicateEmail = Error.Conflict(
        "Identity.User.DuplicateEmail", "A user with this email already exists for this tenant.");

    /// <summary>
    /// Deliberately generic — never reveals whether the email exists vs. the password was
    /// wrong, so login failures don't leak account existence.
    /// </summary>
    public static readonly Error InvalidCredentials = Error.Unauthorized(
        "Identity.User.InvalidCredentials", "The email or password is incorrect.");

    public static readonly Error InvalidRefreshToken = Error.Unauthorized(
        "Identity.User.InvalidRefreshToken", "The refresh token is invalid, expired, or already used.");

    public static readonly Error InvalidBootstrapToken = Error.Unauthorized(
        "Identity.User.InvalidBootstrapToken", "The bootstrap token is invalid, expired, or already used.");

    public static readonly Error BootstrapTokenAlreadyConsumed = Error.Conflict(
        "Identity.User.BootstrapTokenAlreadyConsumed", "This bootstrap token has already been used.");

    /// <summary>
    /// First-run setup is a one-time window: once any user exists, the anonymous
    /// create-first-admin endpoint is permanently closed.
    /// </summary>
    public static readonly Error SetupAlreadyCompleted = Error.Conflict(
        "Identity.User.SetupAlreadyCompleted", "Initial setup has already been completed — sign in instead.");
}
