using Microsoft.EntityFrameworkCore;
using NorthernLink.Drivers.Application.Credentials.SetImage;
using NorthernLink.Drivers.Infrastructure.Persistence;
using Xunit;

namespace NorthernLink.Drivers.IntegrationTests;

/// <summary>
/// The full credential-photo flow the endpoint composes: bytes into object storage, the
/// SetImage command through the real handler + repository + DbContext, one worker poll,
/// then the read row carries the key the stored bytes are retrievable under.
/// </summary>
[Collection("postgres")]
public class CredentialImageFlowTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Stored_image_is_referenced_by_the_projected_read_row_and_retrievable_by_its_key()
    {
        var worker = fixture.BuildDriversProjectionWorker();
        var storage = new FakeObjectStorage();
        var driver = TestDriverFactory.CreateDriver(PostgresFixture.TenantA);
        var credential = TestDriverFactory.CreateCredential(PostgresFixture.TenantA, driver.Id);

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.Drivers.Add(driver);
            writer.DriverCredentials.Add(credential);
            await writer.SaveChangesAsync();
        }

        await worker.ProcessOnceAsync(CancellationToken.None);

        // 1. Store the bytes (the endpoint does this before dispatching the command).
        var imageKey = $"driver-credentials/{PostgresFixture.TenantA}/{driver.Id}/{credential.Id}/original.jpg";
        byte[] imageBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
        await storage.PutAsync(imageKey, new MemoryStream(imageBytes), "image/jpeg");

        // 2. Dispatch the real command through the real handler + repository.
        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var handler = new SetDriverCredentialImageCommandHandler(new DriverCredentialRepository(writer));
            var result = await handler.Handle(
                new SetDriverCredentialImageCommand(
                    PostgresFixture.TenantA, credential.Id, driver.Id, imageKey, "image/jpeg"),
                CancellationToken.None);
            Assert.True(result.IsSuccess);
        }

        // 3. One poll projects the image reference into the read row.
        await worker.ProcessOnceAsync(CancellationToken.None);

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var projected = await reader.DriverCredentialReadModels.SingleAsync(c => c.Id == credential.Id);
        Assert.Equal(imageKey, projected.ImageKey);
        Assert.Equal("image/jpeg", projected.ImageContentType);

        // 4. The key the read row carries resolves to the stored bytes.
        await using var stream = await storage.GetAsync(projected.ImageKey!);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        Assert.Equal(imageBytes, buffer.ToArray());
    }
}
