using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Clearances.Grant;
using NorthernLink.Drivers.Domain.Clearances;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Drivers.Tests;

public class DriverClearanceTests
{
    private static readonly Guid DriverId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void Valid_clearance_is_granted()
    {
        var result = DriverClearance.Grant(
            TestDrivers.TenantId,
            DriverId,
            "  Site Induction  ",
            " Alamos Gold — Lynn Lake ",
            new DateOnly(2027, 5, 1));

        Assert.True(result.IsSuccess);
        var clearance = result.Value;
        Assert.Equal(TestDrivers.TenantId, clearance.TenantId);
        Assert.Equal(DriverId, clearance.DriverId);
        Assert.Equal("Site Induction", clearance.Title);
        Assert.Equal("Alamos Gold — Lynn Lake", clearance.ClientName);
        Assert.Equal(new DateOnly(2027, 5, 1), clearance.Expiry);
        Assert.NotEqual(default, clearance.GrantedAtUtc);
        Assert.Empty(clearance.DomainEvents);
    }

    [Fact]
    public void Missing_title_is_rejected()
    {
        var result = DriverClearance.Grant(TestDrivers.TenantId, DriverId, " ", "Alamos Gold", null);

        Assert.True(result.IsFailure);
        Assert.Equal(ClearanceErrors.TitleRequired, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Missing_client_name_is_rejected()
    {
        var result = DriverClearance.Grant(TestDrivers.TenantId, DriverId, "Site Induction", "", null);

        Assert.True(result.IsFailure);
        Assert.Equal(ClearanceErrors.ClientNameRequired, result.Error);
    }

    [Fact]
    public async Task Granting_a_clearance_for_an_unknown_driver_fails_with_driver_not_found()
    {
        var handler = new GrantDriverClearanceCommandHandler(new FakeClearanceRepository(driverExists: false));

        var result = await handler.Handle(
            new GrantDriverClearanceCommand(TestDrivers.TenantId, DriverId, "Site Induction", "Alamos Gold", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DriverErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Granting_a_clearance_for_an_existing_driver_persists_it()
    {
        var repository = new FakeClearanceRepository(driverExists: true);
        var handler = new GrantDriverClearanceCommandHandler(repository);

        var result = await handler.Handle(
            new GrantDriverClearanceCommand(TestDrivers.TenantId, DriverId, "Site Induction", "Alamos Gold", null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var added = Assert.Single(repository.Added);
        Assert.Equal(result.Value, added.Id);
        Assert.True(repository.Saved);
    }

    private sealed class FakeClearanceRepository(bool driverExists) : IDriverClearanceRepository
    {
        public List<DriverClearance> Added { get; } = [];

        public bool Saved { get; private set; }

        public Task<DriverClearance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Added.FirstOrDefault(c => c.Id == id));

        public Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default) =>
            Task.FromResult(driverExists);

        public void Add(DriverClearance clearance) => Added.Add(clearance);

        public void Remove(DriverClearance clearance) => Added.Remove(clearance);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }
}
