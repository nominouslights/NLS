using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NorthernLink.Identity.Application.Auth.BootstrapAdmin;
using NorthernLink.Identity.Application.Auth.GenerateBootstrapToken;
using NorthernLink.Identity.Infrastructure.Persistence;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Identity.IntegrationTests;

/// <summary>
/// End-to-end proof that a non-Owner account can actually be created: mint an invite naming a
/// role, redeem it, and read the persisted role back out of Postgres.
///
/// This is what makes US-6.0.1's acceptance criterion testable at all. Before invites carried a
/// role, both creation paths hardcoded "Admin" and there was no way to produce a Dispatcher
/// account to reject.
/// </summary>
[Collection("postgres")]
public class BootstrapRoleTests(PostgresFixture fixture)
{
    private static readonly TestTokenIssuer TokenIssuer = new();

    private async Task<string> MintInviteAsync(Guid tenantId, string role)
    {
        await using var context = fixture.CreateContext(tenantId);
        var handler = new GenerateBootstrapTokenCommandHandler(
            new AdminBootstrapTokenRepository(context), TokenIssuer);

        var result = await handler.Handle(
            new GenerateBootstrapTokenCommand(tenantId, role), CancellationToken.None);

        Assert.True(result.IsSuccess, $"Minting the invite failed: {result.Error.Code}");
        return result.Value.Token;
    }

    private async Task<string> ReadRoleAsync(Guid userId)
    {
        await using var connection = await fixture.OpenSystemConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT role FROM identity.users WHERE id = @id;", connection);
        command.Parameters.AddWithValue("id", userId);

        return await command.ExecuteScalarAsync() as string
            ?? throw new InvalidOperationException($"No identity.users row for {userId}.");
    }

    [Theory]
    [InlineData(Roles.Dispatcher)]
    [InlineData(Roles.Accountant)]
    [InlineData(Roles.Supervisor)]
    public async Task Redeeming_an_invite_persists_the_role_it_was_minted_with(string role)
    {
        var rawToken = await MintInviteAsync(SeedTenant.Id, role);

        await using var context = fixture.CreateContext(SeedTenant.Id);
        var handler = new BootstrapAdminCommandHandler(
            new AdminBootstrapTokenRepository(context),
            new UserRepository(context),
            new TestPasswordHasher(),
            TokenIssuer);

        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@northernlink.ca";
        var result = await handler.Handle(
            new BootstrapAdminCommand(rawToken, email, "some password"), CancellationToken.None);

        Assert.True(result.IsSuccess, $"Redemption failed: {(result.IsFailure ? result.Error.Code : "")}");
        Assert.Equal(role, await ReadRoleAsync(result.Value));
    }

    [Fact]
    public async Task An_invite_naming_an_unknown_role_is_never_persisted()
    {
        await using var context = fixture.CreateContext(SeedTenant.Id);
        var handler = new GenerateBootstrapTokenCommandHandler(
            new AdminBootstrapTokenRepository(context), TokenIssuer);

        var result = await handler.Handle(
            new GenerateBootstrapTokenCommand(SeedTenant.Id, "Bookkeeper"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.User.InvalidRole", result.Error.Code);

        // Nothing reached the database — the role column is NOT NULL and the value would have
        // stranded whoever redeemed it.
        await using var readback = fixture.CreateContext(SeedTenant.Id);
        Assert.False(await readback.Set<Domain.Users.AdminBootstrapToken>()
            .AnyAsync(t => t.Role == "Bookkeeper"));
    }

    private sealed class TestPasswordHasher : Application.Abstractions.IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";

        public bool Verify(string password, string hash) => Hash(password) == hash;
    }

    /// <summary>
    /// Opaque-token issuance identical to the real JwtAccessTokenIssuer, minus its constructor's
    /// dependency on the Identity__JwtSigningKey environment variable — this suite exercises the
    /// invite/redeem round trip, never an access token.
    /// </summary>
    private sealed class TestTokenIssuer : Application.Abstractions.IAccessTokenIssuer
    {
        public Application.Abstractions.IssuedAccessToken IssueAccessToken(Domain.Users.User user) =>
            throw new NotSupportedException("These tests never mint an access token.");

        public Application.Abstractions.IssuedOpaqueToken IssueOpaqueToken(TimeSpan lifetime)
        {
            var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            return new Application.Abstractions.IssuedOpaqueToken(
                raw, HashOpaqueToken(raw), DateTimeOffset.UtcNow.Add(lifetime));
        }

        public string HashOpaqueToken(string rawToken) =>
            Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }
}
