using NorthernLink.Identity.Application.Abstractions;
using NorthernLink.Identity.Domain.Users;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Identity.Application.Auth.GenerateBootstrapToken;

public sealed class GenerateBootstrapTokenCommandHandler(
    IAdminBootstrapTokenRepository bootstrapTokenRepository,
    IAccessTokenIssuer accessTokenIssuer)
    : ICommandHandler<GenerateBootstrapTokenCommand, GenerateBootstrapTokenResponse>
{
    public async Task<Result<GenerateBootstrapTokenResponse>> Handle(
        GenerateBootstrapTokenCommand command, CancellationToken cancellationToken)
    {
        // Validate before minting: an invite carrying an unknown role would hash, persist and be
        // handed out, only to fail at redemption when User.Create rejects it — by which point the
        // token is spent and the holder has no way to tell what went wrong.
        var role = command.Role?.Trim();
        if (string.IsNullOrWhiteSpace(role) || !Roles.IsKnown(role))
        {
            return Result.Failure<GenerateBootstrapTokenResponse>(UserErrors.InvalidRole);
        }

        var opaque = accessTokenIssuer.IssueOpaqueToken(BootstrapTokenPolicy.Lifetime);
        var token = AdminBootstrapToken.Issue(command.TenantId, opaque.TokenHash, opaque.ExpiresAtUtc, role);

        bootstrapTokenRepository.Add(token);
        await bootstrapTokenRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(new GenerateBootstrapTokenResponse(opaque.RawToken, opaque.ExpiresAtUtc));
    }
}
