using Microsoft.Extensions.Logging;
using NorthernLink.Identity.Application.Abstractions;
using NorthernLink.Identity.Domain.Users;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Identity.Application.Auth.VerifyPassword;

/// <summary>
/// Verifies the caller's own password against the stored hash through the very same
/// <see cref="IPasswordHasher"/> the login path uses (<c>LoginCommandHandler</c>) — there is
/// deliberately exactly one verification implementation on the platform, so a future change of
/// KDF or of the constant-time comparison cannot leave a second, weaker door standing.
/// <para>
/// <b>No throttling, deliberately.</b> The login path has no lockout or rate limiter today, and
/// this endpoint is a strictly smaller target than login: it requires a valid bearer token and
/// checks only the password of the token's own <c>sub</c>, so the blast radius of guessing is
/// the caller's own account — an account they are already signed in to. Inventing a limiter here
/// (and not on login) would be security theatre; when login gains one, this must gain the same.
/// </para>
/// </summary>
public sealed class VerifyPasswordCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ILogger<VerifyPasswordCommandHandler> logger)
    : ICommandHandler<VerifyPasswordCommand>
{
    public async Task<Result> Handle(VerifyPasswordCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Password))
        {
            return Result.Failure(UserErrors.InvalidPassword);
        }

        var user = await userRepository.GetByIdAsync(command.UserId, cancellationToken);

        // Same generic failure whether the token's subject no longer resolves or the password is
        // simply wrong — mirrors LoginCommandHandler, and there is nothing useful to distinguish.
        if (user is null || !passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            // The user id only — never the attempted password, and never the hash.
            logger.LogWarning(
                "Step-up password verification failed for user {UserId}", command.UserId);
            return Result.Failure(UserErrors.InvalidCredentials);
        }

        return Result.Success();
    }
}
