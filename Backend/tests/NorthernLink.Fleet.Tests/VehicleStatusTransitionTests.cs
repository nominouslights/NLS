using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// The full 6×6 transition matrix, exercised through the aggregate itself:
/// ChangeStatus for operational states, Dispose for Sold/Recycled.
/// </summary>
public class VehicleStatusTransitionTests
{
    private static readonly VehicleStatus[] AllStatuses = Enum.GetValues<VehicleStatus>();

    /// <summary>The plan's matrix. Everything not listed here (incl. the diagonal) is illegal.</summary>
    private static readonly HashSet<(VehicleStatus From, VehicleStatus To)> Allowed =
    [
        (VehicleStatus.Active, VehicleStatus.InMaintenance),
        (VehicleStatus.Active, VehicleStatus.OutOfService),
        (VehicleStatus.Active, VehicleStatus.Retired),
        (VehicleStatus.InMaintenance, VehicleStatus.Active),
        (VehicleStatus.InMaintenance, VehicleStatus.OutOfService),
        (VehicleStatus.InMaintenance, VehicleStatus.Retired),
        (VehicleStatus.OutOfService, VehicleStatus.Active),
        (VehicleStatus.OutOfService, VehicleStatus.InMaintenance),
        (VehicleStatus.OutOfService, VehicleStatus.Retired),
        (VehicleStatus.Retired, VehicleStatus.Sold),
        (VehicleStatus.Retired, VehicleStatus.Recycled),
    ];

    public static TheoryData<VehicleStatus, VehicleStatus> AllPairs()
    {
        var data = new TheoryData<VehicleStatus, VehicleStatus>();
        foreach (var from in AllStatuses)
        {
            foreach (var to in AllStatuses)
            {
                if (from != to)
                {
                    data.Add(from, to);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void Transition_matrix_is_enforced_by_the_aggregate(VehicleStatus from, VehicleStatus to)
    {
        var vehicle = TestVehicles.InStatus(from);

        var result = to switch
        {
            VehicleStatus.Sold => vehicle.Dispose(DisposalMethod.Sold, 5_000m),
            VehicleStatus.Recycled => vehicle.Dispose(DisposalMethod.Recycled),
            VehicleStatus.OutOfService => vehicle.ChangeStatus(to, "matrix test reason"),
            _ => vehicle.ChangeStatus(to),
        };

        if (Allowed.Contains((from, to)))
        {
            Assert.True(result.IsSuccess, $"{from} → {to} should be legal but failed: {result.Error.Code}");
            Assert.Equal(to, vehicle.Status);
        }
        else
        {
            Assert.True(result.IsFailure, $"{from} → {to} should be illegal but succeeded.");
            Assert.Equal(from, vehicle.Status);
        }
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void CanTransition_matches_the_matrix(VehicleStatus from, VehicleStatus to)
    {
        Assert.Equal(Allowed.Contains((from, to)), Vehicle.CanTransition(from, to));
    }

    [Fact]
    public void Same_status_is_not_a_transition()
    {
        foreach (var status in AllStatuses)
        {
            Assert.False(Vehicle.CanTransition(status, status));
        }
    }

    [Fact]
    public void Out_of_service_requires_a_reason()
    {
        var vehicle = TestVehicles.InStatus(VehicleStatus.Active);

        var result = vehicle.ChangeStatus(VehicleStatus.OutOfService);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleErrors.StatusReasonRequired, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(VehicleStatus.Active, vehicle.Status);
    }

    [Fact]
    public void ChangeStatus_cannot_be_used_to_sell_a_retired_vehicle()
    {
        var vehicle = TestVehicles.InStatus(VehicleStatus.Retired);

        var result = vehicle.ChangeStatus(VehicleStatus.Sold);

        Assert.True(result.IsFailure);
        Assert.Equal("Fleet.Vehicle.InvalidStatusTransition", result.Error.Code);
        Assert.Equal(VehicleStatus.Retired, vehicle.Status);
    }

    [Fact]
    public void Dispose_before_retirement_fails_with_NotRetired()
    {
        var vehicle = TestVehicles.InStatus(VehicleStatus.Active);

        var result = vehicle.Dispose(DisposalMethod.Sold, 5_000m);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleErrors.NotRetired, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Disposed_vehicles_reject_every_further_change()
    {
        var vehicle = TestVehicles.InStatus(VehicleStatus.Sold);

        Assert.Equal(VehicleErrors.Disposed, vehicle.ChangeStatus(VehicleStatus.Active).Error);
        Assert.Equal(VehicleErrors.Disposed, vehicle.RecordOdometer(vehicle.OdometerKm + 1).Error);
        Assert.Equal(VehicleErrors.Disposed, vehicle.Dispose(DisposalMethod.Recycled).Error);
        Assert.Equal(
            VehicleErrors.Disposed,
            vehicle.UpdateDetails(
                vehicle.UnitNumber, vehicle.Vin, vehicle.Make, vehicle.Model, vehicle.Year,
                vehicle.SeatingCapacity, vehicle.LicencePlate, vehicle.RequiredLicenceClass,
                vehicle.AcquisitionCostCad, vehicle.EndOfLifeKm).Error);
        Assert.Equal(ErrorType.Conflict, VehicleErrors.Disposed.Type);
    }

    [Fact]
    public void Selling_captures_sale_price_and_disposal_timestamp()
    {
        var vehicle = TestVehicles.InStatus(VehicleStatus.Retired);

        var result = vehicle.Dispose(DisposalMethod.Sold, 12_500m, "Sold at auction");

        Assert.True(result.IsSuccess);
        Assert.Equal(VehicleStatus.Sold, vehicle.Status);
        Assert.Equal(12_500m, vehicle.SalePriceCad);
        Assert.NotNull(vehicle.DisposedAtUtc);
    }

    [Fact]
    public void Recycling_does_not_keep_a_sale_price()
    {
        var vehicle = TestVehicles.InStatus(VehicleStatus.Retired);

        var result = vehicle.Dispose(DisposalMethod.Recycled, 999m);

        Assert.True(result.IsSuccess);
        Assert.Equal(VehicleStatus.Recycled, vehicle.Status);
        Assert.Null(vehicle.SalePriceCad);
    }

    [Fact]
    public void Negative_sale_price_is_rejected()
    {
        var vehicle = TestVehicles.InStatus(VehicleStatus.Retired);

        var result = vehicle.Dispose(DisposalMethod.Sold, -1m);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleErrors.InvalidSalePrice, result.Error);
    }
}
