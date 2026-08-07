using NorthernLink.Identity.Application.Abstractions;
using NorthernLink.Identity.Domain.Users;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Identity.Application.Auth.Setup;

public sealed class CreateFirstAdminCommandHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer accessTokenIssuer)
    : ICommandHandler<CreateFirstAdminCommand, LoginResponse>
{
    public async Task<Result<LoginResponse>> Handle(CreateFirstAdminCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Password))
        {
            return Result.Failure<LoginResponse>(UserErrors.InvalidPassword);
        }

        // The first admin belongs to the single Internal tenant, same as the (now opt-in) seeder,
        // and is the Owner by definition — first-run setup is the business owner standing up the
        // platform. Every other role arrives through a bootstrap invite that names it.
        var userResult = User.Create(
            SeedTenant.Id, command.Email, passwordHasher.Hash(command.Password), Roles.Owner);
        if (userResult.IsFailure)
        {
            return Result.Failure<LoginResponse>(userResult.Error);
        }

        var user = userResult.Value;

        // Atomic one-time gate: only succeeds while the users table is empty (advisory-lock
        // guarded). Once any user exists, first-run setup is permanently closed.
        if (!await userRepository.TryAddFirstUserAsync(user, cancellationToken))
        {
            return Result.Failure<LoginResponse>(UserErrors.SetupAlreadyCompleted);
        }

        // Same token issuance as LoginCommandHandler — sign the new admin straight in.
        var accessToken = accessTokenIssuer.IssueAccessToken(user);
        var refreshToken = accessTokenIssuer.IssueOpaqueToken(RefreshTokenPolicy.Lifetime);

        refreshTokenRepository.Add(RefreshToken.Issue(user.Id, refreshToken.TokenHash, refreshToken.ExpiresAtUtc));
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(new LoginResponse(accessToken.Token, refreshToken.RawToken, accessToken.ExpiresAtUtc));
    }
}
