using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Qbo;

namespace NorthernLink.Budgeting.Application.Qbo.Authorize;

/// <summary>
/// Handles <see cref="StartQboAuthorizationCommand"/>. Configuration is checked first — the
/// Intuit client id (to build the URL) and the vault key (touched via
/// <see cref="IQboTokenProtector.CurrentKeyId"/>) — so a server missing either answers
/// NotConfigured <em>before</em> the person is sent to Intuit and back for nothing, and without
/// leaving a state row behind. Expired states for the tenant are swept on the way through — the
/// table never holds more than a handful of rows.
/// </summary>
public sealed class StartQboAuthorizationCommandHandler(
    IQboAuthClient authClient,
    IQboTokenProtector protector,
    IQboOAuthStateStore states,
    TimeProvider clock)
    : ICommandHandler<StartQboAuthorizationCommand, string>
{
    public async Task<Result<string>> Handle(StartQboAuthorizationCommand command, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return Result.Failure<string>(QboConnectionErrors.UserRequired);
        }

        var state = QboOAuthState.GenerateValue();

        string authorizeUrl;
        try
        {
            authorizeUrl = authClient.BuildAuthorizeUrl(state);
            _ = protector.CurrentKeyId;
        }
        catch (Exception exception) when (
            exception is QboAuthException { Failure: QboAuthFailure.NotConfigured } or InvalidOperationException)
        {
            return Result.Failure<string>(QboConnectionErrors.NotConfigured);
        }

        var now = clock.GetUtcNow();
        await states.PurgeExpiredAsync(now, cancellationToken);
        await states.AddAsync(
            new QboOAuthState
            {
                StateHash = QboOAuthState.Hash(state),
                TenantId = command.TenantId,
                UserId = userId,
                CreatedAtUtc = now,
                ExpiresAtUtc = now + QboOAuthState.Lifetime,
            },
            cancellationToken);

        return Result.Success(authorizeUrl);
    }
}
