using NorthernLink.Identity.Application.Abstractions;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Identity.Application.Auth.Logout;

public sealed class LogoutCommandHandler(
    IRefreshTokenRepository refreshTokenRepository,
    IAccessTokenIssuer accessTokenIssuer)
    : ICommandHandler<LogoutCommand>
{
    public async Task<Result> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        var tokenHash = accessTokenIssuer.HashOpaqueToken(command.RefreshToken);
        var existing = await refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (existing is not null)
        {
            existing.Revoke();
            await refreshTokenRepository.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
