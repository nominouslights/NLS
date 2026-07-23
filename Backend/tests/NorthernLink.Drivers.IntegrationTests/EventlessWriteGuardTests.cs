using Microsoft.EntityFrameworkCore;
using Xunit;

namespace NorthernLink.Drivers.IntegrationTests;

/// <summary>
/// The <c>ModuleDbContext</c> eventless-write guard: saving an aggregate that was Added or
/// Modified without raising a domain event must throw instead of silently skipping the
/// event journal (which would leave the read model permanently stale — the exact bug that
/// left rm_driver_credentials empty). Hard deletes are exempt because the pipeline writes
/// a synthetic aggregate-deleted journal row.
/// </summary>
[Collection("postgres")]
public class EventlessWriteGuardTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Saving_a_modified_aggregate_with_no_domain_events_throws()
    {
        var driver = TestDriverFactory.CreateDriver(PostgresFixture.TenantA);

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.Drivers.Add(driver);
            await writer.SaveChangesAsync();
        }

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var tracked = await writer.Drivers.SingleAsync(d => d.Id == driver.Id);

            // Simulate an eventless mutation: force the entry Modified without any Raise —
            // the seam a future mutating method that forgets its domain event would hit.
            writer.Entry(tracked).State = EntityState.Modified;

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => writer.SaveChangesAsync());
            Assert.Contains("without raising a domain event", exception.Message);
            Assert.Contains(nameof(Domain.Drivers.Driver), exception.Message);
        }
    }

    [Fact]
    public async Task Saving_an_added_aggregate_with_no_domain_events_throws()
    {
        var driver = TestDriverFactory.CreateDriver(PostgresFixture.TenantA);
        driver.ClearDomainEvents(); // strip the registration event → eventless insert

        await using var writer = fixture.CreateContext(PostgresFixture.TenantA);
        writer.Drivers.Add(driver);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => writer.SaveChangesAsync());
        Assert.Contains("without raising a domain event", exception.Message);
    }

    [Fact]
    public async Task Hard_deleting_an_aggregate_without_events_is_exempt()
    {
        var driver = TestDriverFactory.CreateDriver(PostgresFixture.TenantA);

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.Drivers.Add(driver);
            await writer.SaveChangesAsync();
        }

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var tracked = await writer.Drivers.SingleAsync(d => d.Id == driver.Id);
            writer.Drivers.Remove(tracked);

            // No throw: the pipeline writes the synthetic aggregate-deleted journal row.
            await writer.SaveChangesAsync();
        }

        await using (var reader = fixture.CreateContext(PostgresFixture.TenantA))
        {
            Assert.False(await reader.Drivers.AnyAsync(d => d.Id == driver.Id));
        }
    }
}
