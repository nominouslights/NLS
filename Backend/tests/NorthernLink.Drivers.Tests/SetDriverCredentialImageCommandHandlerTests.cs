using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Credentials.SetImage;
using NorthernLink.Drivers.Domain.Credentials;
using Xunit;

namespace NorthernLink.Drivers.Tests;

public class SetDriverCredentialImageCommandHandlerTests
{
    private static readonly Guid DriverId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherDriverId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static DriverCredential AddCredential()
    {
        var result = DriverCredential.Add(
            TestDrivers.TenantId,
            DriverId,
            type: "First Aid",
            label: "Standard First Aid & CPR-C",
            issued: new DateOnly(2026, 1, 15),
            expiry: null,
            optional: false,
            note: null);

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static SetDriverCredentialImageCommand Command(
        Guid credentialId,
        Guid driverId,
        string imageKey = "driver-credentials/key/original.jpg",
        string contentType = "image/jpeg") =>
        new(TestDrivers.TenantId, credentialId, driverId, imageKey, contentType);

    [Fact]
    public async Task Sets_the_image_reference_and_saves()
    {
        var credential = AddCredential();
        var repository = new FakeCredentialRepository(credential);
        var handler = new SetDriverCredentialImageCommandHandler(repository);

        var result = await handler.Handle(Command(credential.Id, DriverId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("driver-credentials/key/original.jpg", credential.ImageKey);
        Assert.Equal("image/jpeg", credential.ImageContentType);
        Assert.True(repository.Saved);
    }

    [Fact]
    public async Task Missing_credential_fails_with_not_found()
    {
        var repository = new FakeCredentialRepository();
        var handler = new SetDriverCredentialImageCommandHandler(repository);

        var result = await handler.Handle(Command(Guid.NewGuid(), DriverId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CredentialErrors.NotFound, result.Error);
        Assert.False(repository.Saved);
    }

    [Fact]
    public async Task Credential_belonging_to_another_driver_fails_with_not_found()
    {
        var credential = AddCredential();
        var repository = new FakeCredentialRepository(credential);
        var handler = new SetDriverCredentialImageCommandHandler(repository);

        var result = await handler.Handle(Command(credential.Id, OtherDriverId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CredentialErrors.NotFound, result.Error);
        Assert.Null(credential.ImageKey);
        Assert.False(repository.Saved);
    }

    [Fact]
    public async Task Second_call_replaces_the_image_reference()
    {
        var credential = AddCredential();
        var repository = new FakeCredentialRepository(credential);
        var handler = new SetDriverCredentialImageCommandHandler(repository);

        var first = await handler.Handle(
            Command(credential.Id, DriverId, "driver-credentials/key/original.jpg", "image/jpeg"),
            CancellationToken.None);
        var second = await handler.Handle(
            Command(credential.Id, DriverId, "driver-credentials/key/original.png", "image/png"),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("driver-credentials/key/original.png", credential.ImageKey);
        Assert.Equal("image/png", credential.ImageContentType);
    }

    private sealed class FakeCredentialRepository(params DriverCredential[] credentials) : IDriverCredentialRepository
    {
        private readonly List<DriverCredential> _credentials = [.. credentials];

        public bool Saved { get; private set; }

        public Task<DriverCredential?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_credentials.FirstOrDefault(c => c.Id == id));

        public Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public void Add(DriverCredential credential) => _credentials.Add(credential);

        public void Remove(DriverCredential credential) => _credentials.Remove(credential);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }
}
