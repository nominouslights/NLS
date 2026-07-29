using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>Builds inspections through the single <see cref="VehicleInspection.Enter"/> factory.</summary>
internal static class TestInspections
{
    public static VehicleInspection PreTrip(
        int? odometerKm = 118_204,
        Guid? vehicleId = null,
        string unit = "U-04",
        InspectionSource source = InspectionSource.Dispatcher,
        IReadOnlyList<InspectionDefect>? defects = null,
        IReadOnlyList<InspectionWeather>? weather = null,
        IReadOnlyList<InspectionRoadCondition>? roadConditions = null,
        InspectionVisibility? visibility = InspectionVisibility.Good,
        InspectionFuelLevel? fuelLevel = InspectionFuelLevel.Full)
    {
        var result = VehicleInspection.Enter(
            TestVehicles.TenantId,
            source,
            InspectionType.PreTrip,
            tripNumber: "TR-4818",
            vehicleId,
            unit,
            driverName: "J. Spence",
            enteredBy: null,
            performedAt: DateTimeOffset.UtcNow,
            odometerKm,
            checklistItems: [new InspectionChecklistItem { Group = "Exterior & Mechanical", Item = "Tires", Passed = true }],
            defects: defects ?? [],
            weather: weather ?? [InspectionWeather.Snow, InspectionWeather.ExtremeCold],
            temperatureC: "-31",
            roadConditions: roadConditions ?? [InspectionRoadCondition.SnowCovered, InspectionRoadCondition.Icy],
            visibility,
            roadAdvisories: "Blowing snow past km 40",
            fuelLevel,
            issues: [],
            attestations: [],
            driverSignatureName: null,
            certifiedAt: null,
            fuelAdded: false,
            fuelLitres: null,
            fuelCostCad: null);

        Assert.True(result.IsSuccess, $"PreTrip inspection entry failed: {result.Error.Code}");
        return result.Value;
    }

    public static VehicleInspection PostTrip(
        int? odometerKm = 118_346,
        Guid? vehicleId = null,
        string unit = "U-04",
        InspectionSource source = InspectionSource.Dispatcher,
        IReadOnlyList<InspectionDefect>? defects = null,
        IReadOnlyList<string>? issues = null,
        IReadOnlyList<bool>? attestations = null,
        bool fuelAdded = true)
    {
        var result = VehicleInspection.Enter(
            TestVehicles.TenantId,
            source,
            InspectionType.PostTrip,
            tripNumber: "TR-4818",
            vehicleId,
            unit,
            driverName: "J. Spence",
            enteredBy: null,
            performedAt: DateTimeOffset.UtcNow,
            odometerKm,
            checklistItems: [new InspectionChecklistItem { Item = "Keys returned / secured", Passed = true }],
            defects: defects ?? [],
            weather: [],
            temperatureC: null,
            roadConditions: [],
            visibility: null,
            roadAdvisories: null,
            fuelLevel: null,
            issues: issues ?? ["Windshield chip near passenger side"],
            attestations: attestations ?? [true, true, true],
            driverSignatureName: "J. Spence",
            certifiedAt: DateTimeOffset.UtcNow,
            fuelAdded,
            fuelLitres: fuelAdded ? 92.4m : null,
            fuelCostCad: fuelAdded ? 178.30m : null);

        Assert.True(result.IsSuccess, $"PostTrip inspection entry failed: {result.Error.Code}");
        return result.Value;
    }

    public static InspectionDefect Defect(InspectionDefectSeverity severity) => new()
    {
        Item = "Wipers & washer fluid",
        Severity = severity,
        Note = "test",
    };
}
