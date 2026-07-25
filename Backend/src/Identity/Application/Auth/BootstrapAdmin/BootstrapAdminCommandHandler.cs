using NorthernLink.Identity.Application.Abstractions;
using NorthernLink.Identity.Domain.Users;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Identity.Application.Auth.BootstrapAdmin;

public sealed class BootstrapAdminCommandHandler(
    IAdminBootstrapTokenRepository bootstrapTokenRepository,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer accessTokenIssuer)
    : ICommandHandler<BootstrapAdminCommand, Guid>
{
    public async Task<Result<Guid>> Handle(BootstrapAdminCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Password))
        {
            return Result.Failure<Guid>(UserErrors.InvalidPassword);
        }

        var tokenHash = accessTokenIssuer.HashOpaqueToken(command.Token);

        // Only one tenant exists today (SeedTenant.Id) — see Application/Auth/RefreshTokenPolicy
        // sibling docs and CLAUDE.md. The caller has no session yet, so there's no ambient
        // tenant to resolve this from; the seed tenant is the only candidate.
        var activeToken = await bootstrapTokenRepository.GetActiveAsync(SeedTenant.Id, cancellationToken);

        if (activeToken is null || activeToken.TokenHash != tokenHash)
        {
            return Result.Failure<Guid>(UserErrors.InvalidBootstrapToken);
        }

        // Belt; Consume() below re-checks as suspenders. Same generic error as an unknown
        // token — this endpoint is anonymous, so expiry must not be distinguishable.
        if (activeToken.IsExpired)
        {
            return Result.Failure<Guid>(UserErrors.InvalidBootstrapToken);
        }

        var userResult = User.Create(SeedTenant.Id, command.Email, passwordHasher.Hash(command.Password), "Admin");
        if (userResult.IsFailure)
        {
            return Result.Failure<Guid>(userResult.Error);
        }

        // Friendly pre-check before touching the token: a known-duplicate email fails fast
        // with the token untouched, so the holder can retry with a different address.
        if (await userRepository.GetByEmailAsync(userResult.Value.Email, cancellationToken) is not null)
        {
            return Result.Failure<Guid>(UserErrors.DuplicateEmail);
        }

        var consumeResult = activeToken.Consume();
        if (consumeResult.IsFailure)
        {
            return Result.Failure<Guid>(consumeResult.Error);
        }

        // Same DbContext backs both repositories (scoped DI) — the single commit inside
        // TryAddNewUserAsync persists the new user and the token's consumption atomically.
        // On a duplicate email (the pre-check→insert race), that one commit fails as a
        // whole, so Consume() is never persisted and the token stays redeemable.
        if (!await userRepository.TryAddNewUserAsync(userResult.Value, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.DuplicateEmail);
        }

        return Result.Success(userResult.Value.Id);
    }
}
