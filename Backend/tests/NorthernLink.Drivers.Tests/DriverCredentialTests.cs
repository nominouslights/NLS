using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Credentials.Add;
using NorthernLink.Drivers.Domain.Credentials;
using NorthernLink.Drivers.Domain.Credentials.Events;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Drivers.Tests;

public class DriverCredentialTests
{
    private static readonly Guid DriverId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static Result<DriverCredential> Add(
        string type = "First Aid",
        string label = "Standard First Aid & CPR-C",
        DateOnly? issued = null,
        DateOnly? expiry = null,
        bool optional = false,
        string? note = null) =>
        DriverCredential.Add(
            TestDrivers.TenantId,
            DriverId,
            type,
            label,
            issued ?? new DateOnly(2026, 1, 15),
            expiry,
            optional,
            note);

    [Fact]
    public void Valid_credential_is_added()
    {
        var result = Add(expiry: new DateOnly(2029, 1, 15), note: "  Card on file  ");

        Assert.True(result.IsSuccess);
        var credential = result.Value;
        Assert.Equal(TestDrivers.TenantId, credential.TenantId);
        Assert.Equal(DriverId, credential.DriverId);
        Assert.Equal("First Aid", credential.Type);
        Assert.Equal("Standard First Aid & CPR-C", credential.Label);
        Assert.Equal(new DateOnly(2026, 1, 15), credential.Issued);
        Assert.Equal(new DateOnly(2029, 1, 15), credential.Expiry);
        Assert.False(credential.Optional);
        Assert.Equal("Card on file", credential.Note);

        // Every aggregate write must raise an event — an eventless write would produce no
        // event_journal row and leave rm_driver_credentials silently stale.
        var domainEvent = Assert.IsType<DriverCredentialAddedDomainEvent>(Assert.Single(credential.DomainEvents));
        Assert.Equal(credential.Id, domainEvent.CredentialId);
        Assert.Equal(DriverId, domainEvent.DriverId);
        Assert.Equal(TestDrivers.TenantId, domainEvent.TenantId);
    }

    [Fact]
    public void Missing_type_is_rejected()
    {
        var result = Add(type: " ");

        Assert.True(result.IsFailure);
        Assert.Equal(CredentialErrors.TypeRequired, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Missing_label_is_rejected()
    {
        var result = Add(label: "");

        Assert.True(result.IsFailure);
        Assert.Equal(CredentialErrors.LabelRequired, result.Error);
    }

    [Fact]
    public void Expiry_before_issued_is_rejected()
    {
        var result = Add(issued: new DateOnly(2026, 1, 15), expiry: new DateOnly(2025, 12, 31));

        Assert.True(result.IsFailure);
        Assert.Equal(CredentialErrors.ExpiryBeforeIssued, result.Error);
    }

    [Fact]
    public void Expiry_on_the_issue_date_is_allowed()
    {
        var result = Add(issued: new DateOnly(2026, 1, 15), expiry: new DateOnly(2026, 1, 15));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Adding_a_credential_for_an_unknown_driver_fails_with_driver_not_found()
    {
        var handler = new AddDriverCredentialCommandHandler(new FakeCredentialRepository(driverExists: false));

        var result = await handler.Handle(
            new AddDriverCredentialCommand(
                TestDrivers.TenantId, DriverId, "First Aid", "CPR-C", new DateOnly(2026, 1, 15), null, false, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DriverErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Adding_a_credential_for_an_existing_driver_persists_it()
    {
        var repository = new FakeCredentialRepository(driverExists: true);
        var handler = new AddDriverCredentialCommandHandler(repository);

        var result = await handler.Handle(
            new AddDriverCredentialCommand(
                TestDrivers.TenantId, DriverId, "First Aid", "CPR-C", new DateOnly(2026, 1, 15), null, false, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var added = Assert.Single(repository.Added);
        Assert.Equal(result.Value, added.Id);
        Assert.True(repository.Saved);
    }

    private sealed class FakeCredentialRepository(bool driverExists) : IDriverCredentialRepository
    {
        public List<DriverCredential> Added { get; } = [];

        public bool Saved { get; private set; }

        public Task<DriverCredential?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Added.FirstOrDefault(c => c.Id == id));

        public Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default) =>
            Task.FromResult(driverExists);

        public void Add(DriverCredential credential) => Added.Add(credential);

        public void Remove(DriverCredential credential) => Added.Remove(credential);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }
}
