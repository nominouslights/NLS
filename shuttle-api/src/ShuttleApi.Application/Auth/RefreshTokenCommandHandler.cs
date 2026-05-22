using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Application.Services;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Users;

namespace ShuttleApi.Application.Auth;

internal sealed class RefreshTokenCommandHandler(
    IUserRepository userRepository,
    IJwtTokenService jwtTokenService)
    : IRequestHandler<RefreshTokenCommand, LoginResult>
{
    public async Task<LoginResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByRefreshTokenAsync(request.RefreshToken, cancellationToken)
            ?? throw new UnauthorizedException("Invalid refresh token.");

        if (user.RefreshTokenExpiry <= DateTime.UtcNow)
            throw new UnauthorizedException("Refresh token has expired.");

        var accessToken = jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.Role.ToString());
        var newRefreshToken = jwtTokenService.GenerateRefreshToken();

        user.SetRefreshToken(newRefreshToken, DateTime.UtcNow.AddDays(7));
        await userRepository.UpdateAsync(user, cancellationToken);

        return new LoginResult(accessToken, newRefreshToken, user.Role.ToString());
    }
}
