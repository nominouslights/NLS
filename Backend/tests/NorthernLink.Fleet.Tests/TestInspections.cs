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
        InspectionFuelLevel? fuelLevel = InspectionFuelLevel.Full,
        IReadOnlyList<InspectionChecklistItem>? checklistItems = null,
        string? certificationStatement = null)
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
            checklistItems: checklistItems
                ?? [new InspectionChecklistItem { Group = "Exterior & Mechanical", Item = "Tires", Passed = true }],
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
            fuelCostCad: null,
            certificationStatement);

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
        bool fuelAdded = true,
        IReadOnlyList<InspectionChecklistItem>? checklistItems = null,
        string? certificationStatement = null)
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
            checklistItems: checklistItems
                ?? [new InspectionChecklistItem { Item = "Keys returned / secured", Passed = true }],
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
            fuelCostCad: fuelAdded ? 178.30m : null,
            certificationStatement);

        Assert.True(result.IsSuccess, $"PostTrip inspection entry failed: {result.Error.Code}");
        return result.Value;
    }

    /// <summary>
    /// Amends through the real aggregate method, varying only what the caller cares about.
    /// Shared by the amend tests and the amend-trap regression tests, so both drive exactly the
    /// same call shape.
    /// </summary>
    public static Result AmendWith(
        VehicleInspection inspection,
        IReadOnlyList<InspectionDefect> defects,
        int? odometerKm = 118_400,
        string unit = "U-04",
        string driverName = "J. Spence",
        IReadOnlyList<InspectionChecklistItem>? checklistItems = null,
        string? certificationStatement = null) =>
        inspection.Amend(
            InspectionSource.Dispatcher,
            vehicleId: inspection.VehicleId,
            unit,
            driverName,
            enteredBy: null,
            performedAt: DateTimeOffset.UtcNow,
            odometerKm,
            checklistItems: checklistItems ?? [],
            defects,
            weather: [],
            temperatureC: null,
            roadConditions: [],
            visibility: null,
            roadAdvisories: null,
            fuelLevel: null,
            issues: [],
            attestations: [],
            driverSignatureName: null,
            certifiedAt: null,
            fuelAdded: false,
            fuelLitres: null,
            fuelCostCad: null,
            certificationStatement);

    public static InspectionDefect Defect(InspectionDefectSeverity severity) => new()
    {
        Item = "Wipers & washer fluid",
        Severity = severity,
        Note = "test",
    };

    /// <summary>
    /// One checklist row, in either shape: leave <paramref name="state"/> null for the legacy
    /// two-value row (what everything written before NL-PTI-01 looks like), or set it for a
    /// tri-state row. Deliberately the SAME builder for both — a second fixture for "new" rows
    /// is how old and new data start being tested under different assumptions.
    /// </summary>
    public static InspectionChecklistItem ChecklistItem(
        string item = "Tires",
        bool passed = true,
        ChecklistItemState? state = null,
        string? note = null,
        string? group = "Exterior & Mechanical") => new()
    {
        Group = group,
        Item = item,
        Passed = passed,
        State = state,
        Note = note,
    };
}
