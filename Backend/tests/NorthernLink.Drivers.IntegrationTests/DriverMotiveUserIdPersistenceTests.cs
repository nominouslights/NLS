using Microsoft.EntityFrameworkCore;
using Npgsql;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Infrastructure.Persistence;
using Xunit;

namespace NorthernLink.Drivers.IntegrationTests;

/// <summary>
/// The Motive user id against a real Postgres as the app role: the per-tenant partial unique
/// index's 23505 is translated by <see cref="DriverRepository.TrySaveMotiveUserIdAsync"/> (the
/// lost check-then-save race becomes a domain conflict, not a 500) while any OTHER unique
/// violation still throws, the <see cref="DriverRepository.ExistsByMotiveUserIdAsync"/> query,
/// and deactivation freeing the id for a replacement driver.
/// </summary>
[Collection("postgres")]
public class DriverMotiveUserIdPersistenceTests(PostgresFixture fixture)
{
    private static string NewMotiveId() => $"mu-{Guid.NewGuid():N}";

    private async Task<(Driver Mapped, Driver Other, string MotiveId)> SeedMappedPairAsync()
    {
        var motiveId = NewMotiveId();
        var mapped = TestDriverFactory.CreateDriver(PostgresFixture.TenantA);
        var other = TestDriverFactory.CreateDriver(PostgresFixture.TenantA);
        Assert.True(mapped.SetMotiveUserId(motiveId).IsSuccess);

        await using var writer = fixture.CreateContext(PostgresFixture.TenantA);
        writer.Drivers.AddRange(mapped, other);
        await writer.SaveChangesAsync();
        return (mapped, other, motiveId);
    }

    [Fact]
    public async Task Losing_the_race_to_the_unique_index_is_reported_not_thrown()
    {
        var (_, other, motiveId) = await SeedMappedPairAsync();

        // Skip the handler's existence check on purpose — what a concurrent request that passed
        // its check before the winner committed looks like at save time.
        await using var context = fixture.CreateContext(PostgresFixture.TenantA);
        var repository = new DriverRepository(context);
        var loser = await repository.GetByIdAsync(other.Id);
        Assert.True(loser!.SetMotiveUserId(motiveId).IsSuccess);

        Assert.False(await repository.TrySaveMotiveUserIdAsync());

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var stored = await reader.Drivers.AsNoTracking().SingleAsync(d => d.Id == other.Id);
        Assert.Null(stored.MotiveUserId);
    }

    [Fact]
    public async Task Another_unique_violation_is_not_mislabelled_as_a_motive_conflict()
    {
        var (mapped, other, _) = await SeedMappedPairAsync();
        var userId = Guid.NewGuid();

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var tracked = await writer.Drivers.SingleAsync(d => d.Id == mapped.Id);
            Assert.True(tracked.LinkUser(userId).IsSuccess);
            await writer.SaveChangesAsync();
        }

        // Collide on the Identity-link index instead.
        await using var context = fixture.CreateContext(PostgresFixture.TenantA);
        var repository = new DriverRepository(context);
        var clash = await repository.GetByIdAsync(other.Id);
        Assert.True(clash!.LinkUser(userId).IsSuccess);

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(() => repository.TrySaveMotiveUserIdAsync());
        var postgres = Assert.IsType<PostgresException>(thrown.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.NotEqual(DriverRepository.MotiveUserIdIndexName, postgres.ConstraintName);
    }

    [Fact]
    public async Task The_existence_query_excludes_the_driver_itself_and_other_tenants()
    {
        var (mapped, other, motiveId) = await SeedMappedPairAsync();

        await using var context = fixture.CreateContext(PostgresFixture.TenantA);
        var repository = new DriverRepository(context);
        Assert.True(await repository.ExistsByMotiveUserIdAsync(motiveId, other.Id));
        Assert.False(await repository.ExistsByMotiveUserIdAsync(motiveId, mapped.Id));
        Assert.False(await repository.ExistsByMotiveUserIdAsync(NewMotiveId(), other.Id));

        // An existence query, not a load: nothing is pulled into the change tracker.
        Assert.Empty(context.ChangeTracker.Entries<Driver>());

        await using var tenantB = fixture.CreateContext(PostgresFixture.TenantB);
        Assert.False(await new DriverRepository(tenantB).ExistsByMotiveUserIdAsync(motiveId, Guid.NewGuid()));
    }

    [Fact]
    public async Task Deactivating_frees_the_id_for_the_replacement_driver()
    {
        var (departed, replacement, motiveId) = await SeedMappedPairAsync();

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var tracked = await writer.Drivers.SingleAsync(d => d.Id == departed.Id);
            Assert.True(tracked.ChangeStatus(DriverStatus.Deactivated).IsSuccess);
            await writer.SaveChangesAsync();
        }

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var repository = new DriverRepository(writer);
            Assert.False(await repository.ExistsByMotiveUserIdAsync(motiveId, replacement.Id));
            var tracked = await repository.GetByIdAsync(replacement.Id);
            Assert.True(tracked!.SetMotiveUserId(motiveId).IsSuccess);
            Assert.True(await repository.TrySaveMotiveUserIdAsync());
        }

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        Assert.Null((await reader.Drivers.AsNoTracking().SingleAsync(d => d.Id == departed.Id)).MotiveUserId);
        Assert.Equal(motiveId, (await reader.Drivers.AsNoTracking().SingleAsync(d => d.Id == replacement.Id)).MotiveUserId);
    }
}
