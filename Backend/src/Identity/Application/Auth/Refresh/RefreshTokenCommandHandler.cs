using NorthernLink.Identity.Application.Abstractions;
using NorthernLink.Identity.Domain.Users;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Identity.Application.Auth.Refresh;

public sealed class RefreshTokenCommandHandler(
    IRefreshTokenRepository refreshTokenRepository,
    IUserRepository userRepository,
    IAccessTokenIssuer accessTokenIssuer)
    : ICommandHandler<RefreshTokenCommand, LoginResponse>
{
    public async Task<Result<LoginResponse>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var tokenHash = accessTokenIssuer.HashOpaqueToken(command.RefreshToken);
        var existing = await refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (existing is null || !existing.IsActive)
        {
            return Result.Failure<LoginResponse>(UserErrors.InvalidRefreshToken);
        }

        var user = await userRepository.GetByIdAsync(existing.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<LoginResponse>(UserErrors.InvalidRefreshToken);
        }

        // Rotation: the presented token is single-use — revoke it before issuing the next one.
        existing.Revoke();

        var accessToken = accessTokenIssuer.IssueAccessToken(user);
        var newRefreshToken = accessTokenIssuer.IssueOpaqueToken(RefreshTokenPolicy.Lifetime);
        refreshTokenRepository.Add(RefreshToken.Issue(user.Id, newRefreshToken.TokenHash, newRefreshToken.ExpiresAtUtc));

        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(new LoginResponse(accessToken.Token, newRefreshToken.RawToken, accessToken.ExpiresAtUtc));
    }
}
