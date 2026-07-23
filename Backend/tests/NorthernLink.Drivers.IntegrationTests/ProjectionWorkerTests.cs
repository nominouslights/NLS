using Microsoft.EntityFrameworkCore;
using Xunit;

namespace NorthernLink.Drivers.IntegrationTests;

/// <summary>
/// Drives the real projection worker one poll at a time (no timer) against a real Postgres
/// as the non-superuser app role. These are the regression tests for the stuck-projection
/// bug: <c>DriverCredential.Add/SetImage</c> and <c>DriverClearance.Grant</c> used to raise
/// no domain events, so no journal rows were appended and the rm_* tables stayed empty
/// forever. On a codebase without those events, every test here goes red.
/// </summary>
[Collection("postgres")]
public class ProjectionWorkerTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Adding_a_credential_projects_a_read_row_and_refreshes_the_parent_driver_stats()
    {
        var worker = fixture.BuildDriversProjectionWorker();
        var driver = TestDriverFactory.CreateDriver(PostgresFixture.TenantA);
        var expiry = new DateOnly(2029, 1, 15);

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.Drivers.Add(driver);
            await writer.SaveChangesAsync();
        }

        // Project the driver first so the parent rm_drivers row exists before the
        // credential projection refreshes its denormalized credential stats.
        await worker.ProcessOnceAsync(CancellationToken.None);

        var credential = TestDriverFactory.CreateCredential(PostgresFixture.TenantA, driver.Id, expiry);
        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.DriverCredentials.Add(credential);
            await writer.SaveChangesAsync();
        }

        await worker.ProcessOnceAsync(CancellationToken.None);

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var projected = await reader.DriverCredentialReadModels.SingleAsync(c => c.Id == credential.Id);
        Assert.Equal(driver.Id, projected.DriverId);
        Assert.Equal("First Aid", projected.Type);
        Assert.Equal("Standard First Aid & CPR-C", projected.Label);
        Assert.Equal(expiry, projected.Expiry);
        Assert.Equal(credential.Version, projected.Version);

        var driverRow = await reader.DriverReadModels.SingleAsync(d => d.Id == driver.Id);
        Assert.Equal(1, driverRow.CredentialCount);
        Assert.Equal(expiry, driverRow.SoonestCredentialExpiry);
    }

    [Fact]
    public async Task Setting_the_credential_image_carries_the_image_reference_into_the_read_row()
    {
        var worker = fixture.BuildDriversProjectionWorker();
        var driver = TestDriverFactory.CreateDriver(PostgresFixture.TenantA);
        var credential = TestDriverFactory.CreateCredential(PostgresFixture.TenantA, driver.Id);

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.Drivers.Add(driver);
            writer.DriverCredentials.Add(credential);
            await writer.SaveChangesAsync();
        }

        await worker.ProcessOnceAsync(CancellationToken.None);

        var imageKey = $"driver-credentials/{PostgresFixture.TenantA}/{driver.Id}/{credential.Id}/original.jpg";
        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var tracked = await writer.DriverCredentials.SingleAsync(c => c.Id == credential.Id);
            tracked.SetImage(imageKey, "image/jpeg");
            await writer.SaveChangesAsync();
        }

        await worker.ProcessOnceAsync(CancellationToken.None);

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var projected = await reader.DriverCredentialReadModels.SingleAsync(c => c.Id == credential.Id);
        Assert.Equal(imageKey, projected.ImageKey);
        Assert.Equal("image/jpeg", projected.ImageContentType);
    }

    [Fact]
    public async Task Granting_a_clearance_projects_a_read_row()
    {
        var worker = fixture.BuildDriversProjectionWorker();
        var driver = TestDriverFactory.CreateDriver(PostgresFixture.TenantA);
        var clearance = TestDriverFactory.CreateClearance(PostgresFixture.TenantA, driver.Id);

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.Drivers.Add(driver);
            writer.DriverClearances.Add(clearance);
            await writer.SaveChangesAsync();
        }

        await worker.ProcessOnceAsync(CancellationToken.None);

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var projected = await reader.DriverClearanceReadModels.SingleAsync(c => c.Id == clearance.Id);
        Assert.Equal(driver.Id, projected.DriverId);
        Assert.Equal("Site Induction", projected.Title);
        Assert.Equal("Alamos Gold — Lynn Lake", projected.ClientName);
        Assert.Equal(new DateOnly(2027, 5, 1), projected.Expiry);
    }

    [Fact]
    public async Task Removing_a_credential_removes_its_read_row_and_refreshes_the_parent_driver_stats()
    {
        var worker = fixture.BuildDriversProjectionWorker();
        var driver = TestDriverFactory.CreateDriver(PostgresFixture.TenantA);
        var credential = TestDriverFactory.CreateCredential(
            PostgresFixture.TenantA, driver.Id, expiry: new DateOnly(2029, 1, 15));

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.Drivers.Add(driver);
            writer.DriverCredentials.Add(credential);
            await writer.SaveChangesAsync();
        }

        await worker.ProcessOnceAsync(CancellationToken.None);

        await using (var reader = fixture.CreateContext(PostgresFixture.TenantA))
        {
            Assert.True(await reader.DriverCredentialReadModels.AnyAsync(c => c.Id == credential.Id));
        }

        // Hard-delete the credential. The audit pipeline journals the delete (synthetic
        // aggregate-deleted event), so the next poll drops the read row and recomputes the
        // parent driver's denormalized credential fields.
        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var tracked = await writer.DriverCredentials.SingleAsync(c => c.Id == credential.Id);
            writer.DriverCredentials.Remove(tracked);
            await writer.SaveChangesAsync();
        }

        await worker.ProcessOnceAsync(CancellationToken.None);

        await using (var reader = fixture.CreateContext(PostgresFixture.TenantA))
        {
            Assert.False(await reader.DriverCredentialReadModels.AnyAsync(c => c.Id == credential.Id));

            var driverRow = await reader.DriverReadModels.SingleAsync(d => d.Id == driver.Id);
            Assert.Equal(0, driverRow.CredentialCount);
            Assert.Null(driverRow.SoonestCredentialExpiry);
        }
    }
}
