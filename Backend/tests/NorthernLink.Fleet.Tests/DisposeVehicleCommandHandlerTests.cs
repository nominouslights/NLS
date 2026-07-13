using NorthernLink.Fleet.Application.Vehicles.Dispose;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Fleet.Tests;

public class DisposeVehicleCommandHandlerTests
{
    [Fact]
    public async Task Unknown_vehicle_is_not_found()
    {
        var repository = new InMemoryVehicleRepository();
        var handler = new DisposeVehicleCommandHandler(repository);

        var result = await handler.Handle(
            new DisposeVehicleCommand(TestVehicles.TenantId, Guid.NewGuid(), DisposalMethod.Sold, 1m, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Disposing_an_active_vehicle_fails_with_NotRetired()
    {
        var repository = new InMemoryVehicleRepository();
        var vehicle = TestVehicles.Create();
        repository.Add(vehicle);
        var handler = new DisposeVehicleCommandHandler(repository);

        var result = await handler.Handle(
            new DisposeVehicleCommand(TestVehicles.TenantId, vehicle.Id, DisposalMethod.Sold, 5_000m, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleErrors.NotRetired, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(VehicleStatus.Active, vehicle.Status);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Selling_a_retired_vehicle_persists_method_price_and_timestamp()
    {
        var repository = new InMemoryVehicleRepository();
        var vehicle = TestVehicles.InStatus(VehicleStatus.Retired);
        repository.Add(vehicle);
        var handler = new DisposeVehicleCommandHandler(repository);

        var result = await handler.Handle(
            new DisposeVehicleCommand(TestVehicles.TenantId, vehicle.Id, DisposalMethod.Sold, 14_250m, "Sold to Miller the Mover"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(VehicleStatus.Sold, vehicle.Status);
        Assert.Equal(14_250m, vehicle.SalePriceCad);
        Assert.NotNull(vehicle.DisposedAtUtc);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Disposing_twice_is_a_conflict()
    {
        var repository = new InMemoryVehicleRepository();
        var vehicle = TestVehicles.InStatus(VehicleStatus.Sold);
        repository.Add(vehicle);
        var handler = new DisposeVehicleCommandHandler(repository);

        var result = await handler.Handle(
            new DisposeVehicleCommand(TestVehicles.TenantId, vehicle.Id, DisposalMethod.Recycled, null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleErrors.Disposed, result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public async Task Recycling_a_retired_vehicle_succeeds_without_a_price()
    {
        var repository = new InMemoryVehicleRepository();
        var vehicle = TestVehicles.InStatus(VehicleStatus.Retired);
        repository.Add(vehicle);
        var handler = new DisposeVehicleCommandHandler(repository);

        var result = await handler.Handle(
            new DisposeVehicleCommand(TestVehicles.TenantId, vehicle.Id, DisposalMethod.Recycled, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(VehicleStatus.Recycled, vehicle.Status);
        Assert.Null(vehicle.SalePriceCad);
    }
}
