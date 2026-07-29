using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Inspections.Events;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// The domain-level <see cref="VehicleInspection.Amend"/> correction path: it re-derives
/// <see cref="VehicleInspection.Result"/> from the corrected defects, keeps identity
/// (<c>Type</c>/<c>TripNumber</c>) fixed, re-runs the same validation as Enter, and raises
/// <see cref="VehicleInspectionAmendedDomainEvent"/>.
/// </summary>
public class AmendInspectionTests
{
    private static Result AmendWith(
        VehicleInspection inspection,
        IReadOnlyList<InspectionDefect> defects,
        int? odometerKm = 118_400,
        string unit = "U-04",
        string driverName = "J. Spence") =>
        inspection.Amend(
            InspectionSource.Dispatcher,
            vehicleId: inspection.VehicleId,
            unit,
            driverName,
            enteredBy: null,
            performedAt: DateTimeOffset.UtcNow,
            odometerKm,
            checklistItems: [],
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
            fuelCostCad: null);

    [Fact]
    public void Amending_in_an_out_of_service_defect_flips_a_pass_to_a_fail()
    {
        var inspection = TestInspections.PreTrip(defects: []); // no defects -> Pass
        Assert.Equal(InspectionResult.Pass, inspection.Result);

        var result = AmendWith(inspection, [TestInspections.Defect(InspectionDefectSeverity.OutOfService)]);

        Assert.True(result.IsSuccess);
        Assert.Equal(InspectionResult.Fail, inspection.Result);
    }

    [Fact]
    public void Amending_out_the_defects_flips_a_fail_back_to_a_pass()
    {
        var inspection = TestInspections.PreTrip(defects: [TestInspections.Defect(InspectionDefectSeverity.Major)]);
        Assert.Equal(InspectionResult.Fail, inspection.Result);

        var result = AmendWith(inspection, defects: []);

        Assert.True(result.IsSuccess);
        Assert.Equal(InspectionResult.Pass, inspection.Result);
    }

    [Fact]
    public void Amend_keeps_type_and_trip_number_immutable()
    {
        var inspection = TestInspections.PostTrip();
        var originalType = inspection.Type;
        var originalTrip = inspection.TripNumber;

        var result = AmendWith(inspection, [TestInspections.Defect(InspectionDefectSeverity.Minor)]);

        Assert.True(result.IsSuccess);
        Assert.Equal(originalType, inspection.Type);
        Assert.Equal(originalTrip, inspection.TripNumber);
        Assert.Equal(InspectionType.PostTrip, inspection.Type);
        Assert.Equal("TR-4818", inspection.TripNumber);
    }

    [Fact]
    public void Amend_updates_the_editable_fields()
    {
        var inspection = TestInspections.PreTrip(odometerKm: 118_204);

        var result = AmendWith(inspection, defects: [], odometerKm: 118_999, driverName: "R. Ballantyne");

        Assert.True(result.IsSuccess);
        Assert.Equal(118_999, inspection.OdometerKm);
        Assert.Equal("R. Ballantyne", inspection.DriverName);
    }

    [Fact]
    public void Amend_raises_the_amended_domain_event()
    {
        var inspection = TestInspections.PreTrip();
        inspection.ClearDomainEvents(); // drop the create event so the amend event stands alone

        var result = AmendWith(inspection, defects: []);

        Assert.True(result.IsSuccess);
        var amended = Assert.Single(inspection.DomainEvents.OfType<VehicleInspectionAmendedDomainEvent>());
        Assert.Equal(inspection.Id, amended.InspectionId);
        Assert.Equal(inspection.TenantId, amended.TenantId);
    }

    [Fact]
    public void Amend_re_runs_validation_and_rejects_a_blank_unit_without_mutating()
    {
        var inspection = TestInspections.PreTrip(odometerKm: 118_204);
        inspection.ClearDomainEvents();

        var result = AmendWith(inspection, defects: [], odometerKm: 118_999, unit: "  ");

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.UnitRequired, result.Error);
        // Rejected before any mutation — the original reading and no amend event.
        Assert.Equal(118_204, inspection.OdometerKm);
        Assert.Empty(inspection.DomainEvents.OfType<VehicleInspectionAmendedDomainEvent>());
    }
}
