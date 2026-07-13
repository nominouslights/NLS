using NorthernLink.Fleet.Application.Vehicles.RecordOdometer;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Fleet.Tests;

public class RecordOdometerCommandHandlerTests
{
    [Fact]
    public async Task Unknown_vehicle_is_not_found()
    {
        var repository = new InMemoryVehicleRepository();
        var handler = new RecordOdometerCommandHandler(repository);

        var result = await handler.Handle(
            new RecordOdometerCommand(TestVehicles.TenantId, Guid.NewGuid(), 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Normal_reading_updates_the_odometer_without_retiring()
    {
        var repository = new InMemoryVehicleRepository();
        var vehicle = TestVehicles.Create(odometerKm: 100_000, endOfLifeKm: 500_000);
        repository.Add(vehicle);
        var handler = new RecordOdometerCommandHandler(repository);

        var result = await handler.Handle(
            new RecordOdometerCommand(TestVehicles.TenantId, vehicle.Id, 150_000),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(150_000, vehicle.OdometerKm);
        Assert.Equal(VehicleStatus.Active, vehicle.Status);
        Assert.Empty(repository.Certificates);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Rollback_is_rejected_and_nothing_is_saved()
    {
        var repository = new InMemoryVehicleRepository();
        var vehicle = TestVehicles.Create(odometerKm: 100_000);
        repository.Add(vehicle);
        var handler = new RecordOdometerCommandHandler(repository);

        var result = await handler.Handle(
            new RecordOdometerCommand(TestVehicles.TenantId, vehicle.Id, 90_000),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleErrors.OdometerRollback, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(100_000, vehicle.OdometerKm);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Crossing_end_of_life_auto_retires_and_issues_a_certificate()
    {
        var repository = new InMemoryVehicleRepository();
        var vehicle = TestVehicles.Create(odometerKm: 495_000, acquisitionCostCad: 175_000m, endOfLifeKm: 500_000);
        repository.Add(vehicle);
        var handler = new RecordOdometerCommandHandler(repository);

        var result = await handler.Handle(
            new RecordOdometerCommand(TestVehicles.TenantId, vehicle.Id, 501_000),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(VehicleStatus.Retired, vehicle.Status);
        Assert.Equal(0m, vehicle.CurrentValueCad);

        var certificate = Assert.Single(repository.Certificates);
        Assert.Equal(vehicle.Id, certificate.VehicleId);
        Assert.Equal(501_000, certificate.FinalOdometerKm);;
    }

    [Fact]
    public async Task Further_readings_on_a_retired_vehicle_do_not_issue_another_certificate()
    {
        var repository = new InMemoryVehicleRepository();
        var vehicle = TestVehicles.Create(odometerKm: 495_000, endOfLifeKm: 500_000);
        repository.Add(vehicle);
        var handler = new RecordOdometerCommandHandler(repository);

        _ = await handler.Handle(
            new RecordOdometerCommand(TestVehicles.TenantId, vehicle.Id, 500_000),
            CancellationToken.None);
        var second = await handler.Handle(
            new RecordOdometerCommand(TestVehicles.TenantId, vehicle.Id, 500_500),
            CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Single(repository.Certificates);
    }
}
