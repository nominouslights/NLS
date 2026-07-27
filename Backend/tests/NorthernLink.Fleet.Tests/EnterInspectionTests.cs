using NorthernLink.Fleet.Domain.Inspections;
using Xunit;

namespace NorthernLink.Fleet.Tests;

public class EnterInspectionTests
{
    [Fact]
    public void Pre_trip_entry_carries_the_weather_road_fuel_and_odometer_in_sections()
    {
        var vehicleId = Guid.NewGuid();

        var inspection = TestInspections.PreTrip(odometerKm: 118_204, vehicleId: vehicleId);

        Assert.Equal(InspectionType.PreTrip, inspection.Type);
        Assert.Equal(InspectionSource.Dispatcher, inspection.Source);
        Assert.Equal(vehicleId, inspection.VehicleId);
        Assert.Equal("TR-4818", inspection.TripNumber);
        Assert.Null(inspection.ManifestId);
        Assert.Equal(118_204, inspection.OdometerKm);
        Assert.Equal([InspectionWeather.Snow, InspectionWeather.ExtremeCold], inspection.Weather);
        Assert.Equal([InspectionRoadCondition.SnowCovered, InspectionRoadCondition.Icy], inspection.RoadConditions);
        Assert.Equal(InspectionVisibility.Good, inspection.Visibility);
        Assert.Equal(InspectionFuelLevel.Full, inspection.FuelLevel);
        Assert.Equal("-31", inspection.TemperatureC);
        // Post-trip-only sections stay empty on a pre-trip.
        Assert.Empty(inspection.Issues);
        Assert.Empty(inspection.Attestations);
        Assert.Null(inspection.CertifiedAt);
    }

    [Fact]
    public void Post_trip_entry_carries_the_issues_certification_and_fuel_added_sections()
    {
        var inspection = TestInspections.PostTrip(odometerKm: 118_346);

        Assert.Equal(InspectionType.PostTrip, inspection.Type);
        Assert.Equal(118_346, inspection.OdometerKm);
        Assert.Equal(["Windshield chip near passenger side"], inspection.Issues);
        Assert.Equal([true, true, true], inspection.Attestations);
        Assert.Equal("J. Spence", inspection.DriverSignatureName);
        Assert.NotNull(inspection.CertifiedAt);
        Assert.True(inspection.FuelAdded);
        Assert.Equal(92.4m, inspection.FuelLitres);
        Assert.Equal(178.30m, inspection.FuelCostCad);
        // Pre-trip-only sections stay empty on a post-trip.
        Assert.Empty(inspection.Weather);
        Assert.Null(inspection.FuelLevel);
    }

    [Fact]
    public void Driver_app_entry_keeps_entered_by_null_while_dispatcher_defaults_it()
    {
        var driverApp = TestInspections.PreTrip(source: InspectionSource.DriverApp);
        var dispatcher = TestInspections.PreTrip(source: InspectionSource.Dispatcher);

        Assert.Null(driverApp.EnteredBy);
        Assert.Equal("Dispatch", dispatcher.EnteredBy);
    }

    [Fact]
    public void Entry_requires_a_unit()
    {
        var result = VehicleInspection.Enter(
            TestVehicles.TenantId,
            InspectionSource.Dispatcher,
            InspectionType.PreTrip,
            tripNumber: null,
            vehicleId: null,
            unit: "  ",
            driverName: "J. Spence",
            enteredBy: null,
            performedAt: DateTimeOffset.UtcNow,
            odometerKm: 100,
            checklistItems: [],
            defects: [],
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
            fuelCostCad: null);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.UnitRequired, result.Error);
    }

    [Fact]
    public void A_post_trip_out_of_service_defect_derives_a_failed_inspection()
    {
        var inspection = TestInspections.PostTrip(defects: [TestInspections.Defect(InspectionDefectSeverity.OutOfService)]);

        Assert.Equal(InspectionResult.Fail, inspection.Result);
    }
}
