using NorthernLink.Identity.Application.Auth;
using NorthernLink.Identity.Application.Auth.GenerateBootstrapToken;
using NorthernLink.Identity.Domain.Users;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Identity.Tests;

public class GenerateBootstrapTokenCommandHandlerTests
{
    [Fact]
    public async Task Stores_the_hash_never_the_raw_token()
    {
        var tokenRepository = new InMemoryAdminBootstrapTokenRepository();
        var handler = new GenerateBootstrapTokenCommandHandler(tokenRepository, new FakeAccessTokenIssuer());

        var result = await handler.Handle(new GenerateBootstrapTokenCommand(SeedTenant.Id, Roles.Owner), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(tokenRepository.Tokens);
        Assert.Equal(SeedTenant.Id, stored.TenantId);
        Assert.NotEqual(result.Value.Token, stored.TokenHash);
        Assert.Equal($"hashed:{result.Value.Token}", stored.TokenHash);
        Assert.Equal(1, tokenRepository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Expiry_is_the_policy_lifetime_from_now()
    {
        var tokenRepository = new InMemoryAdminBootstrapTokenRepository();
        var handler = new GenerateBootstrapTokenCommandHandler(tokenRepository, new FakeAccessTokenIssuer());

        var before = DateTimeOffset.UtcNow;
        var result = await handler.Handle(new GenerateBootstrapTokenCommand(SeedTenant.Id, Roles.Owner), CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(tokenRepository.Tokens);
        Assert.InRange(stored.ExpiresAtUtc, before.Add(BootstrapTokenPolicy.Lifetime), after.Add(BootstrapTokenPolicy.Lifetime));
        Assert.False(stored.IsExpired);
    }

    [Fact]
    public async Task Response_carries_the_raw_token_and_its_expiry()
    {
        var tokenRepository = new InMemoryAdminBootstrapTokenRepository();
        var handler = new GenerateBootstrapTokenCommandHandler(tokenRepository, new FakeAccessTokenIssuer());

        var result = await handler.Handle(new GenerateBootstrapTokenCommand(SeedTenant.Id, Roles.Owner), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrEmpty(result.Value.Token));
        var stored = Assert.Single(tokenRepository.Tokens);
        Assert.Equal(stored.ExpiresAtUtc, result.Value.ExpiresAtUtc);
    }

    [Fact]
    public async Task Stores_the_requested_role_on_the_token()
    {
        var tokenRepository = new InMemoryAdminBootstrapTokenRepository();
        var handler = new GenerateBootstrapTokenCommandHandler(tokenRepository, new FakeAccessTokenIssuer());

        var result = await handler.Handle(
            new GenerateBootstrapTokenCommand(SeedTenant.Id, Roles.Dispatcher), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(tokenRepository.Tokens);
        Assert.Equal(Roles.Dispatcher, stored.Role);
    }

    // The invite is validated at mint time rather than at redemption: an invite carrying a bad
    // role would otherwise be handed out and only fail once spent, with the holder locked out and
    // no way to tell why.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Bookkeeper")]
    [InlineData("owner")] // right role, wrong case — RequireRole is case-sensitive
    [InlineData(Roles.LegacyAdmin)] // no new user may be created as "Admin"
    public async Task Unknown_role_fails_and_persists_nothing(string role)
    {
        var tokenRepository = new InMemoryAdminBootstrapTokenRepository();
        var handler = new GenerateBootstrapTokenCommandHandler(tokenRepository, new FakeAccessTokenIssuer());

        var result = await handler.Handle(
            new GenerateBootstrapTokenCommand(SeedTenant.Id, role), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.InvalidRole, result.Error);
        Assert.Empty(tokenRepository.Tokens);
        Assert.Equal(0, tokenRepository.SaveChangesCallCount);
    }
}
