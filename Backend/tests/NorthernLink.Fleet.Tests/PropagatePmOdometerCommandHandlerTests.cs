using NorthernLink.Fleet.Application.Maintenance.Completions.PropagateOdometer;
using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Fleet.Domain.Vehicles;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// The PM twin of PropagateInspectionOdometerCommandHandlerTests: a logged completion's
/// odometer reading advances the vehicle master odometer (monotonic — historical entries
/// no-op), auto-retire applies at end of life, and every miss is a tolerant no-op.
/// </summary>
public class PropagatePmOdometerCommandHandlerTests
{
    private static PropagatePmOdometerCommandHandler Handler(
        InMemoryPmCompletionRepository completions,
        InMemoryVehicleRepository vehicles) => new(completions, vehicles);

    private static PmCompletion LogCompletion(Guid vehicleId, int odometerKm)
    {
        var result = PmCompletion.Log(
            TestVehicles.TenantId,
            vehicleId,
            planId: Guid.NewGuid(),
            itemCode: "PM-E-001",
            PmEntryKind.Item,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
            odometerKm,
            performedBy: "R. Thomas",
            workOrderId: null,
            measurement: null,
            notes: null);

        Assert.True(result.IsSuccess, $"Test completion creation failed: {result.Error.Code}");
        return result.Value;
    }

    [Fact]
    public async Task A_completion_ahead_of_the_vehicle_reading_advances_the_odometer()
    {
        var vehicles = new InMemoryVehicleRepository();
        var vehicle = TestVehicles.Create(odometerKm: 118_000, endOfLifeKm: 500_000);
        vehicles.Add(vehicle);

        var completions = new InMemoryPmCompletionRepository();
        var completion = LogCompletion(vehicle.Id, odometerKm: 120_500);
        completions.Add(completion);

        var result = await Handler(completions, vehicles)
            .Handle(new PropagatePmOdometerCommand(TestVehicles.TenantId, completion.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(120_500, vehicle.OdometerKm);
        Assert.Equal(1, vehicles.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_historical_completion_below_the_vehicle_reading_is_a_no_op()
    {
        var vehicles = new InMemoryVehicleRepository();
        var vehicle = TestVehicles.Create(odometerKm: 200_000, endOfLifeKm: 500_000);
        vehicles.Add(vehicle);

        var completions = new InMemoryPmCompletionRepository();
        var completion = LogCompletion(vehicle.Id, odometerKm: 118_000);
        completions.Add(completion);

        var result = await Handler(completions, vehicles)
            .Handle(new PropagatePmOdometerCommand(TestVehicles.TenantId, completion.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(200_000, vehicle.OdometerKm);
        Assert.Equal(0, vehicles.SaveChangesCallCount);
    }

    [Fact]
    public async Task Crossing_end_of_life_auto_retires_and_issues_a_certificate()
    {
        var vehicles = new InMemoryVehicleRepository();
        var vehicle = TestVehicles.Create(odometerKm: 495_000, acquisitionCostCad: 175_000m, endOfLifeKm: 500_000);
        vehicles.Add(vehicle);

        var completions = new InMemoryPmCompletionRepository();
        var completion = LogCompletion(vehicle.Id, odometerKm: 501_000);
        completions.Add(completion);

        var result = await Handler(completions, vehicles)
            .Handle(new PropagatePmOdometerCommand(TestVehicles.TenantId, completion.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(VehicleStatus.Retired, vehicle.Status);
        var certificate = Assert.Single(vehicles.Certificates);
        Assert.Equal(vehicle.Id, certificate.VehicleId);
        Assert.Equal(501_000, certificate.FinalOdometerKm);
    }

    [Fact]
    public async Task An_unknown_completion_is_a_graceful_no_op()
    {
        var vehicles = new InMemoryVehicleRepository();
        vehicles.Add(TestVehicles.Create());

        var result = await Handler(new InMemoryPmCompletionRepository(), vehicles)
            .Handle(new PropagatePmOdometerCommand(TestVehicles.TenantId, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, vehicles.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_completion_for_a_missing_vehicle_is_a_graceful_no_op()
    {
        var vehicles = new InMemoryVehicleRepository();

        var completions = new InMemoryPmCompletionRepository();
        var completion = LogCompletion(Guid.NewGuid(), odometerKm: 120_000);
        completions.Add(completion);

        var result = await Handler(completions, vehicles)
            .Handle(new PropagatePmOdometerCommand(TestVehicles.TenantId, completion.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, vehicles.SaveChangesCallCount);
    }
}
