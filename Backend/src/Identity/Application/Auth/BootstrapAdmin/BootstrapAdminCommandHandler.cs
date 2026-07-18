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

        var consumeResult = activeToken.Consume();
        if (consumeResult.IsFailure)
        {
            return Result.Failure<Guid>(consumeResult.Error);
        }

        var userResult = User.Create(SeedTenant.Id, command.Email, passwordHasher.Hash(command.Password), "Admin");
        if (userResult.IsFailure)
        {
            return Result.Failure<Guid>(userResult.Error);
        }

        userRepository.Add(userResult.Value);

        // Same DbContext backs both repositories (scoped DI) — one SaveChangesAsync call
        // commits the new user and the token's consumption atomically.
        await userRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(userResult.Value.Id);
    }
}
