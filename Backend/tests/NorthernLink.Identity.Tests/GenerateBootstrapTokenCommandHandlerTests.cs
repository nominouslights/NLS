using NorthernLink.Identity.Application.Auth;
using NorthernLink.Identity.Application.Auth.GenerateBootstrapToken;
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

        var result = await handler.Handle(new GenerateBootstrapTokenCommand(SeedTenant.Id), CancellationToken.None);

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
        var result = await handler.Handle(new GenerateBootstrapTokenCommand(SeedTenant.Id), CancellationToken.None);
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

        var result = await handler.Handle(new GenerateBootstrapTokenCommand(SeedTenant.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrEmpty(result.Value.Token));
        var stored = Assert.Single(tokenRepository.Tokens);
        Assert.Equal(stored.ExpiresAtUtc, result.Value.ExpiresAtUtc);
    }
}
