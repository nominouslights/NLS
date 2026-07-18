using NorthernLink.Identity.Application.Abstractions;
using NorthernLink.Identity.Domain.Users;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Identity.Application.Auth.Login;

public sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer accessTokenIssuer)
    : ICommandHandler<LoginCommand, LoginResponse>
{
    public async Task<Result<LoginResponse>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(command.Email, cancellationToken);

        // Deliberately identical failure whether the email doesn't exist or the password is
        // wrong — never leak account existence through the error.
        if (user is null || !passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            return Result.Failure<LoginResponse>(UserErrors.InvalidCredentials);
        }

        var accessToken = accessTokenIssuer.IssueAccessToken(user);
        var refreshToken = accessTokenIssuer.IssueOpaqueToken(RefreshTokenPolicy.Lifetime);

        refreshTokenRepository.Add(RefreshToken.Issue(user.Id, refreshToken.TokenHash, refreshToken.ExpiresAtUtc));
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(new LoginResponse(accessToken.Token, refreshToken.RawToken, accessToken.ExpiresAtUtc));
    }
}
