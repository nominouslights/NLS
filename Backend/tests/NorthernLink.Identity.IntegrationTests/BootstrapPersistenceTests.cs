using Npgsql;
using NorthernLink.Identity.Domain.Users;
using NorthernLink.Identity.Infrastructure.Persistence;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Identity.IntegrationTests;

/// <summary>
/// Proves the duplicate-email catch in <see cref="UserRepository.TryAddNewUserAsync"/>
/// matches the real IX_users_email unique index, and that the single failed commit rolls
/// back the token consumption pending on the same unit of work — the token stays
/// redeemable after a duplicate-email redemption attempt.
/// </summary>
[Collection("postgres")]
public class BootstrapPersistenceTests(PostgresFixture fixture)
{
    private static User CreateUser(string email)
    {
        var result = User.Create(SeedTenant.Id, email, $"hashed:pw-{email}", "Admin");
        Assert.True(result.IsSuccess, $"Test user creation failed: {result.Error.Code}");
        return result.Value;
    }

    private static AdminBootstrapToken IssueToken(Guid tenantId) =>
        AdminBootstrapToken.Issue(
            tenantId,
            $"hashed:{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddMinutes(15));

    [Fact]
    public async Task Duplicate_email_hits_the_real_unique_index_and_keeps_the_token_alive()
    {
        // Unique per test so this collection's tests never step on each other's rows.
        var email = $"dup-{Guid.NewGuid():N}@northernlink.ca";
        var tokenTenantId = Guid.NewGuid();

        // Seed the already-existing user (anonymous-flow repo, system session) and an active
        // bootstrap token (minted on a tenant session, like the real authenticated endpoint —
        // RLS admits the INSERT via the tenant policy, not the system escape hatch).
        await using (var userSeedContext = fixture.CreateContext(tenantId: null))
        {
            Assert.True(await new UserRepository(userSeedContext).TryAddNewUserAsync(CreateUser(email)));
        }

        await using (var tokenSeedContext = fixture.CreateContext(tokenTenantId))
        {
            var tokenRepository = new AdminBootstrapTokenRepository(tokenSeedContext);
            tokenRepository.Add(IssueToken(tokenTenantId));
            await tokenRepository.SaveChangesAsync();
        }

        // The redemption race path: one unit of work carrying both the token consumption
        // and the duplicate user insert — exactly what BootstrapAdminCommandHandler builds
        // when the pre-check misses a concurrent insert.
        Guid tokenId;
        await using (var context = fixture.CreateContext(tenantId: null))
        {
            var token = await new AdminBootstrapTokenRepository(context).GetActiveAsync(tokenTenantId);
            Assert.NotNull(token);
            tokenId = token.Id;
            Assert.True(token.Consume().IsSuccess);

            Assert.False(await new UserRepository(context).TryAddNewUserAsync(CreateUser(email)));
        }

        await using var connection = await fixture.OpenSystemConnectionAsync();

        // The failed commit persisted nothing: still exactly one user with the email…
        await using (var userCount = new NpgsqlCommand(
            "SELECT count(*) FROM identity.users WHERE email = @email;", connection))
        {
            userCount.Parameters.AddWithValue("email", email);
            Assert.Equal(1L, await userCount.ExecuteScalarAsync());
        }

        // …and the token's consumption rolled back with it — consumed_at_utc is still NULL.
        await using var consumedAt = new NpgsqlCommand(
            "SELECT consumed_at_utc FROM identity.admin_bootstrap_tokens WHERE id = @id;", connection);
        consumedAt.Parameters.AddWithValue("id", tokenId);
        Assert.Equal(DBNull.Value, await consumedAt.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Fresh_email_commits_the_user_and_the_token_consumption_atomically()
    {
        var email = $"new-{Guid.NewGuid():N}@northernlink.ca";
        var tokenTenantId = Guid.NewGuid();

        // Minted on a tenant session, like the real authenticated endpoint.
        await using (var tokenSeedContext = fixture.CreateContext(tokenTenantId))
        {
            var tokenRepository = new AdminBootstrapTokenRepository(tokenSeedContext);
            tokenRepository.Add(IssueToken(tokenTenantId));
            await tokenRepository.SaveChangesAsync();
        }

        Guid tokenId;
        await using (var context = fixture.CreateContext(tenantId: null))
        {
            var token = await new AdminBootstrapTokenRepository(context).GetActiveAsync(tokenTenantId);
            Assert.NotNull(token);
            tokenId = token.Id;
            Assert.True(token.Consume().IsSuccess);

            Assert.True(await new UserRepository(context).TryAddNewUserAsync(CreateUser(email)));
        }

        await using var connection = await fixture.OpenSystemConnectionAsync();

        await using (var userCount = new NpgsqlCommand(
            "SELECT count(*) FROM identity.users WHERE email = @email;", connection))
        {
            userCount.Parameters.AddWithValue("email", email);
            Assert.Equal(1L, await userCount.ExecuteScalarAsync());
        }

        await using var consumedAt = new NpgsqlCommand(
            "SELECT consumed_at_utc FROM identity.admin_bootstrap_tokens WHERE id = @id;", connection);
        consumedAt.Parameters.AddWithValue("id", tokenId);
        Assert.NotEqual(DBNull.Value, await consumedAt.ExecuteScalarAsync());
    }
}
