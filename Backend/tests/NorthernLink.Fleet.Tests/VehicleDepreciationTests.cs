using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Domain.Vehicles.Events;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>Straight-line km depreciation, monotonic odometer, and end-of-life auto-retire.</summary>
public class VehicleDepreciationTests
{
    [Fact]
    public void Value_at_zero_km_is_the_full_acquisition_cost()
    {
        var vehicle = TestVehicles.Create(odometerKm: 0, acquisitionCostCad: 100_000m, endOfLifeKm: 500_000);

        Assert.Equal(100_000m, vehicle.CurrentValueCad);
        Assert.Equal(500_000, vehicle.RemainingKm);
        Assert.Equal(0m, vehicle.LifeUsedPct);
    }

    [Fact]
    public void Value_at_half_life_is_half_the_acquisition_cost()
    {
        var vehicle = TestVehicles.Create(odometerKm: 250_000, acquisitionCostCad: 100_000m, endOfLifeKm: 500_000);

        Assert.Equal(50_000m, vehicle.CurrentValueCad);
        Assert.Equal(250_000, vehicle.RemainingKm);
        Assert.Equal(50m, vehicle.LifeUsedPct);
    }

    [Fact]
    public void Value_at_or_past_end_of_life_is_zero()
    {
        var atLimit = TestVehicles.Create(odometerKm: 0, acquisitionCostCad: 80_000m, endOfLifeKm: 300_000);
        _ = atLimit.RecordOdometer(300_000);
        Assert.Equal(0m, atLimit.CurrentValueCad);

        var pastLimit = TestVehicles.Create(odometerKm: 0, acquisitionCostCad: 80_000m, endOfLifeKm: 300_000);
        _ = pastLimit.RecordOdometer(340_000);
        Assert.Equal(0m, pastLimit.CurrentValueCad);
        Assert.Equal(0, pastLimit.RemainingKm);
        Assert.Equal(100m, pastLimit.LifeUsedPct); // capped
    }

    [Fact]
    public void Odometer_cannot_roll_back()
    {
        var vehicle = TestVehicles.Create(odometerKm: 100_000);

        var result = vehicle.RecordOdometer(99_999);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleErrors.OdometerRollback, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(100_000, vehicle.OdometerKm);
    }

    [Fact]
    public void Crossing_end_of_life_auto_retires_the_vehicle()
    {
        var vehicle = TestVehicles.Create(odometerKm: 490_000, endOfLifeKm: 500_000);

        var result = vehicle.RecordOdometer(500_100);

        Assert.True(result.IsSuccess);
        Assert.Equal(VehicleStatus.Retired, vehicle.Status);
        Assert.Equal(Vehicle.EndOfLifeRetirementReason, vehicle.StatusReason);
        Assert.Equal(0m, vehicle.CurrentValueCad);
        Assert.Contains(vehicle.DomainEvents, e => e is VehicleReachedEndOfLifeDomainEvent);
        Assert.Contains(vehicle.DomainEvents, e =>
            e is VehicleStatusChangedDomainEvent changed && changed.NewStatus == VehicleStatus.Retired);
    }

    [Fact]
    public void Odometer_update_below_end_of_life_does_not_retire()
    {
        var vehicle = TestVehicles.Create(odometerKm: 100_000, endOfLifeKm: 500_000);

        var result = vehicle.RecordOdometer(200_000);

        Assert.True(result.IsSuccess);
        Assert.Equal(VehicleStatus.Active, vehicle.Status);
        Assert.Equal(200_000, vehicle.OdometerKm);
    }

    [Fact]
    public void Already_retired_vehicle_does_not_retire_twice_on_further_readings()
    {
        var vehicle = TestVehicles.InStatus(VehicleStatus.Retired);
        vehicle.ClearDomainEvents();

        var result = vehicle.RecordOdometer(vehicle.EndOfLifeKm + 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(VehicleStatus.Retired, vehicle.Status);
        Assert.DoesNotContain(vehicle.DomainEvents, e => e is VehicleReachedEndOfLifeDomainEvent);
    }
}
