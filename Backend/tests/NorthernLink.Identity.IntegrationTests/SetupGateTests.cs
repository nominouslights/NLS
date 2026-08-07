using Npgsql;
using NorthernLink.Identity.Domain.Users;
using NorthernLink.Identity.Infrastructure.Persistence;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Identity.IntegrationTests;

/// <summary>
/// The real advisory-lock proof for the first-run gate: two genuinely concurrent
/// <see cref="UserRepository.TryAddFirstUserAsync"/> calls on separate connections must
/// serialize on pg_advisory_xact_lock so exactly one wins — no unique-index luck involved
/// (the two racers use different emails on purpose).
/// </summary>
[Collection("postgres")]
public class SetupGateTests(PostgresFixture fixture)
{
    private static User CreateUser(string email)
    {
        var result = User.Create(SeedTenant.Id, email, $"hashed:pw-{email}", Roles.Owner);
        Assert.True(result.IsSuccess, $"Test user creation failed: {result.Error.Code}");
        return result.Value;
    }

    [Fact]
    public async Task Concurrent_first_admin_attempts_let_exactly_one_win()
    {
        await fixture.ResetUsersAsync();

        await using var contextA = fixture.CreateContext(tenantId: null);
        await using var contextB = fixture.CreateContext(tenantId: null);
        var repositoryA = new UserRepository(contextA);
        var repositoryB = new UserRepository(contextB);

        var results = await Task.WhenAll(
            repositoryA.TryAddFirstUserAsync(CreateUser("racer-a@northernlink.ca")),
            repositoryB.TryAddFirstUserAsync(CreateUser("racer-b@northernlink.ca")));

        Assert.Equal(1, results.Count(won => won));

        await using (var connection = await fixture.OpenSystemConnectionAsync())
        {
            await using var command = new NpgsqlCommand("SELECT count(*) FROM identity.users;", connection);
            Assert.Equal(1L, await command.ExecuteScalarAsync());
        }

        // The gate stays closed: a later, uncontended retry still returns false.
        await using var contextC = fixture.CreateContext(tenantId: null);
        var repositoryC = new UserRepository(contextC);
        Assert.False(await repositoryC.TryAddFirstUserAsync(CreateUser("racer-c@northernlink.ca")));
    }
}
