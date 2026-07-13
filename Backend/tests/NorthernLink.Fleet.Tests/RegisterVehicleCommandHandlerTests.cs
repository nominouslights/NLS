using NorthernLink.Fleet.Application.Vehicles.Register;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Fleet.Tests;

public class RegisterVehicleCommandHandlerTests
{
    private static RegisterVehicleCommand ValidCommand(
        string vin = "1FTBW2CM4PKA79013",
        string unitNumber = "U-07",
        int seatingCapacity = 7) => new(
        TestVehicles.TenantId,
        unitNumber,
        vin,
        "Ford",
        "Transit T-150",
        2023,
        seatingCapacity,
        "MB · FRT 900",
        "Class 4",
        0,
        62_000m,
        350_000);

    [Fact]
    public async Task Registering_a_vehicle_persists_it_and_returns_its_id()
    {
        var repository = new InMemoryVehicleRepository();
        var handler = new RegisterVehicleCommandHandler(repository);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var vehicle = Assert.Single(repository.Vehicles);
        Assert.Equal(result.Value, vehicle.Id);
        Assert.Equal(TestVehicles.TenantId, vehicle.TenantId);
        Assert.Equal(VehicleStatus.Active, vehicle.Status);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Duplicate_vin_is_a_conflict()
    {
        var repository = new InMemoryVehicleRepository();
        repository.Add(TestVehicles.Create(vin: "1FTBW2CM4PKA79013", unitNumber: "U-01"));
        var handler = new RegisterVehicleCommandHandler(repository);

        var result = await handler.Handle(ValidCommand(vin: "1FTBW2CM4PKA79013"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleErrors.DuplicateVin, result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Duplicate_unit_number_is_a_conflict()
    {
        var repository = new InMemoryVehicleRepository();
        repository.Add(TestVehicles.Create(unitNumber: "U-07"));
        var handler = new RegisterVehicleCommandHandler(repository);

        var result = await handler.Handle(ValidCommand(unitNumber: "U-07"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleErrors.DuplicateUnitNumber, result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public async Task Invalid_vin_is_a_validation_failure()
    {
        var repository = new InMemoryVehicleRepository();
        var handler = new RegisterVehicleCommandHandler(repository);

        var result = await handler.Handle(ValidCommand(vin: "NOT-A-VIN"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleErrors.InvalidVin, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Empty(repository.Vehicles);
    }

    [Fact]
    public async Task Non_positive_seating_capacity_is_rejected()
    {
        var repository = new InMemoryVehicleRepository();
        var handler = new RegisterVehicleCommandHandler(repository);

        var result = await handler.Handle(ValidCommand(seatingCapacity: 0), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleErrors.InvalidCapacity, result.Error);
    }
}
