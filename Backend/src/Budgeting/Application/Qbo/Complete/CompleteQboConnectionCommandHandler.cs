using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Qbo;

namespace NorthernLink.Budgeting.Application.Qbo.Complete;

/// <summary>
/// Handles <see cref="CompleteQboConnectionCommand"/>, in an order chosen so that every refusal
/// happens as early, and as cheaply, as it can:
/// <list type="number">
/// <item><description>The state: it must exist <em>for this tenant</em> (another tenant's state
/// is invisible — query filter and RLS), belong to this user, be unspent and unexpired, and then
/// be spent by one conditional UPDATE. Every mismatch is the same <c>StateInvalid</c>, so the
/// answer never reveals whose state it was.</description></item>
/// <item><description>The different-company rule, which needs only the realm id from the
/// callback — checked before a token exists.</description></item>
/// <item><description>The token exchange, then the company's name and home currency.</description></item>
/// <item><description>The aggregate's own rules (CAD). A refusal from here on holds a live
/// grant the platform will not keep, so it is revoked at Intuit, best effort.</description></item>
/// <item><description>Vault first, then the connection, so an Active connection never exists
/// without tokens behind it.</description></item>
/// </list>
/// </summary>
public sealed class CompleteQboConnectionCommandHandler(
    IQboOAuthStateStore states,
    IQboConnectionRepository connections,
    IQboAuthClient authClient,
    IQboAccountingClient accountingClient,
    IQboTokenStore tokens,
    TimeProvider clock,
    ILogger<CompleteQboConnectionCommandHandler> logger)
    : ICommandHandler<CompleteQboConnectionCommand>
{
    public async Task<Result> Handle(CompleteQboConnectionCommand command, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return Result.Failure(QboConnectionErrors.UserRequired);
        }

        if (string.IsNullOrWhiteSpace(command.Code)
            || string.IsNullOrWhiteSpace(command.State)
            || string.IsNullOrWhiteSpace(command.RealmId))
        {
            return Result.Failure(QboConnectionErrors.CallbackIncomplete);
        }

        var realmId = command.RealmId.Trim();
        if (!QboConnection.IsValidRealmId(realmId))
        {
            return Result.Failure(QboConnectionErrors.RealmIdInvalid);
        }

        // 1. The state.
        var now = clock.GetUtcNow();
        var stateHash = QboOAuthState.Hash(command.State);
        var state = await states.FindAsync(stateHash, cancellationToken);

        if (state is null
            || state.TenantId != command.TenantId
            || state.UserId != userId
            || state.ConsumedAtUtc is not null)
        {
            return Result.Failure(QboConnectionErrors.StateInvalid);
        }

        if (state.ExpiresAtUtc <= now)
        {
            return Result.Failure(QboConnectionErrors.StateExpired);
        }

        if (!await states.TryConsumeAsync(stateHash, now, cancellationToken))
        {
            return Result.Failure(QboConnectionErrors.StateInvalid);
        }

        // 2. Same company as before, if there was a before.
        var existing = await connections.GetAllAsync(cancellationToken);
        var sameCompany = QboConnection.EnsureSameCompany(existing, realmId);
        if (sameCompany.IsFailure)
        {
            return sameCompany;
        }

        // 3. Tokens, then what the company is.
        QboTokenGrant grant;
        try
        {
            grant = await authClient.ExchangeCodeAsync(command.Code, cancellationToken);
        }
        catch (QboAuthException exception)
        {
            logger.LogWarning(
                "QuickBooks token exchange failed for tenant {TenantId}: {Failure} — {Reason}",
                command.TenantId, exception.Failure, exception.Message);
            return Result.Failure(exception.Failure == QboAuthFailure.NotConfigured
                ? QboConnectionErrors.NotConfigured
                : QboConnectionErrors.TokenExchangeFailed);
        }

        QboCompanyInfo company;
        string? homeCurrency;
        try
        {
            company = await accountingClient.GetCompanyInfoAsync(realmId, grant.AccessToken, cancellationToken);
            homeCurrency = await accountingClient.GetHomeCurrencyAsync(realmId, grant.AccessToken, cancellationToken);
        }
        catch (QboApiException exception)
        {
            logger.LogWarning(
                "QuickBooks company lookup failed for tenant {TenantId}, realm {RealmId}: {Failure} — {Reason}",
                command.TenantId, realmId, exception.Failure, exception.Message);
            await RevokeQuietlyAsync(grant, command.TenantId, cancellationToken);
            return Result.Failure(QboConnectionErrors.CompanyLookupFailed);
        }

        // 4. The aggregate's rules.
        var environment = Enum.Parse<QboEnvironment>(accountingClient.Environment, ignoreCase: true);
        var target = existing.FirstOrDefault(c => string.Equals(c.RealmId, realmId, StringComparison.Ordinal));

        Result applied;
        if (target is null)
        {
            var created = QboConnection.Connect(
                command.TenantId, realmId, company.CompanyName, environment, homeCurrency,
                userId, grant.RefreshTokenExpiresAtUtc, now);
            if (created.IsSuccess)
            {
                connections.Add(created.Value);
            }

            applied = created;
        }
        else
        {
            applied = target.Reconnect(
                realmId, company.CompanyName, environment, homeCurrency,
                userId, grant.RefreshTokenExpiresAtUtc, now);
        }

        if (applied.IsFailure)
        {
            await RevokeQuietlyAsync(grant, command.TenantId, cancellationToken);
            return applied;
        }

        // 5. Vault, then connection.
        await tokens.StoreGrantAsync(command.TenantId, realmId, grant, cancellationToken);
        await connections.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task RevokeQuietlyAsync(QboTokenGrant grant, Guid tenantId, CancellationToken cancellationToken)
    {
        try
        {
            await authClient.RevokeAsync(grant.RefreshToken, cancellationToken);
        }
        catch (QboAuthException exception)
        {
            // Best effort: an unrevoked grant for a company we refused simply expires unused.
            logger.LogWarning(
                "Revoking a refused QuickBooks grant failed for tenant {TenantId}: {Failure} — {Reason}",
                tenantId, exception.Failure, exception.Message);
        }
    }
}
