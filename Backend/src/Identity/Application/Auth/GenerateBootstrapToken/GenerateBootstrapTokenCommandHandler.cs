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
        var opaque = accessTokenIssuer.IssueOpaqueToken(BootstrapTokenPolicy.Lifetime);
        var token = AdminBootstrapToken.Issue(command.TenantId, opaque.TokenHash, opaque.ExpiresAtUtc);

        bootstrapTokenRepository.Add(token);
        await bootstrapTokenRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(new GenerateBootstrapTokenResponse(opaque.RawToken, opaque.ExpiresAtUtc));
    }
}
