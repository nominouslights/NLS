using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Qbo;

namespace NorthernLink.Budgeting.Application.Qbo.Disconnect;

/// <summary>
/// Handles <see cref="DisconnectQboCommand"/>. The revoke at Intuit is best effort: Intuit being
/// down, or the grant already revoked from the QuickBooks side, must never leave a person unable
/// to disconnect. What is not best effort is the local half — the tokens are deleted and the
/// connection is Disconnected whatever Intuit said, so the platform stops using the grant.
/// </summary>
public sealed class DisconnectQboCommandHandler(
    IQboConnectionRepository connections,
    IQboTokenStore tokens,
    IQboAuthClient authClient,
    TimeProvider clock,
    ILogger<DisconnectQboCommandHandler> logger)
    : ICommandHandler<DisconnectQboCommand>
{
    public async Task<Result> Handle(DisconnectQboCommand command, CancellationToken cancellationToken)
    {
        var connection = await connections.GetLiveAsync(cancellationToken);
        if (connection is null)
        {
            return Result.Failure(QboConnectionErrors.NotConnected);
        }

        var refreshToken = await tokens.ReadRefreshTokenAsync(command.TenantId, cancellationToken);
        if (refreshToken is not null)
        {
            try
            {
                await authClient.RevokeAsync(refreshToken, cancellationToken);
            }
            catch (QboAuthException exception)
            {
                logger.LogWarning(
                    "Revoking the QuickBooks grant failed for tenant {TenantId}; disconnecting locally anyway: {Failure} — {Reason}",
                    command.TenantId, exception.Failure, exception.Message);
            }
        }

        var disconnected = connection.Disconnect(command.ActorId, clock.GetUtcNow());
        if (disconnected.IsFailure)
        {
            return disconnected;
        }

        await tokens.DeleteAsync(command.TenantId, cancellationToken);
        await connections.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
