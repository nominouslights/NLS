using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Application.Services;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Users;

namespace ShuttleApi.Application.Auth;

internal sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService)
    : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(request.Email.ToLowerInvariant(), cancellationToken)
            ?? throw new UnauthorizedException("Invalid email or password.");

        if (!user.IsActive)
            throw new UnauthorizedException("Account is inactive.");

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid email or password.");

        var accessToken = jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.Role.ToString());
        var refreshToken = jwtTokenService.GenerateRefreshToken();

        user.SetRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7));
        await userRepository.UpdateAsync(user, cancellationToken);

        return new LoginResult(accessToken, refreshToken, user.Role.ToString());
    }
}
