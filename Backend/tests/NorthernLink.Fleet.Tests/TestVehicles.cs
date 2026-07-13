using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>Factory helpers to build vehicles in any lifecycle state.</summary>
internal static class TestVehicles
{
    public static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public const string ValidVin = "1HVBBAAN8RH553310";

    public static Vehicle Create(
        int odometerKm = 100_000,
        decimal acquisitionCostCad = 100_000m,
        int endOfLifeKm = 500_000,
        string unitNumber = "U-99",
        string vin = ValidVin,
        int seatingCapacity = 24)
    {
        var result = Vehicle.Register(
            TenantId,
            unitNumber,
            Vin.Create(vin).Value,
            "International",
            "3000",
            2019,
            seatingCapacity,
            "MB · GHT 999",
            "Class 4",
            odometerKm,
            acquisitionCostCad,
            endOfLifeKm);

        Assert.True(result.IsSuccess, $"Test vehicle registration failed: {result.Error.Code}");
        return result.Value;
    }

    /// <summary>Drives a fresh vehicle into the requested lifecycle state via domain methods.</summary>
    public static Vehicle InStatus(VehicleStatus status)
    {
        var vehicle = Create();

        var result = status switch
        {
            VehicleStatus.Active => Result.Success(),
            VehicleStatus.InMaintenance => vehicle.ChangeStatus(VehicleStatus.InMaintenance),
            VehicleStatus.OutOfService => vehicle.ChangeStatus(VehicleStatus.OutOfService, "test reason"),
            VehicleStatus.Retired => vehicle.ChangeStatus(VehicleStatus.Retired, "test retirement"),
            VehicleStatus.Sold => Then(
                vehicle.ChangeStatus(VehicleStatus.Retired),
                () => vehicle.Dispose(DisposalMethod.Sold, 10_000m)),
            VehicleStatus.Recycled => Then(
                vehicle.ChangeStatus(VehicleStatus.Retired),
                () => vehicle.Dispose(DisposalMethod.Recycled)),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };

        Assert.True(result.IsSuccess, $"Could not drive test vehicle into {status}: {result.Error.Code}");
        Assert.Equal(status, vehicle.Status);
        return vehicle;
    }

    private static Result Then(Result first, Func<Result> second) =>
        first.IsFailure ? first : second();
}
