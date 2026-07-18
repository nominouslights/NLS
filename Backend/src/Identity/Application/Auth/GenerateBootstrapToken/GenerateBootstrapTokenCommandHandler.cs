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
    /// <summary>
    /// Bootstrap tokens don't carry their own expiry column (<see cref="AdminBootstrapToken"/>
    /// only tracks issued/consumed) — this lifetime only sizes the opaque-token generator's
    /// shared record shape and is otherwise unused.
    /// </summary>
    private static readonly TimeSpan NominalLifetime = TimeSpan.FromDays(365);

    public async Task<Result<GenerateBootstrapTokenResponse>> Handle(
        GenerateBootstrapTokenCommand command, CancellationToken cancellationToken)
    {
        var opaque = accessTokenIssuer.IssueOpaqueToken(NominalLifetime);
        var token = AdminBootstrapToken.Issue(command.TenantId, opaque.TokenHash);

        bootstrapTokenRepository.Add(token);
        await bootstrapTokenRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(new GenerateBootstrapTokenResponse(opaque.RawToken));
    }
}
