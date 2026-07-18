using NorthernLink.Fleet.Domain.Inspections;
using Xunit;

namespace NorthernLink.Fleet.Tests;

public class VehicleInspectionResultTests
{
    private static InspectionDefect Defect(InspectionDefectSeverity severity) => new()
    {
        Item = "Wipers & washer fluid",
        Severity = severity,
        Note = "test",
    };

    [Fact]
    public void No_defects_derive_a_pass()
    {
        Assert.Equal(InspectionResult.Pass, VehicleInspection.DeriveResult([]));
    }

    [Fact]
    public void Only_minor_defects_derive_pass_with_defects()
    {
        var result = VehicleInspection.DeriveResult(
            [Defect(InspectionDefectSeverity.Minor), Defect(InspectionDefectSeverity.Minor)]);

        Assert.Equal(InspectionResult.PassWithDefects, result);
    }

    [Fact]
    public void A_major_defect_derives_a_fail()
    {
        var result = VehicleInspection.DeriveResult(
            [Defect(InspectionDefectSeverity.Minor), Defect(InspectionDefectSeverity.Major)]);

        Assert.Equal(InspectionResult.Fail, result);
    }

    [Fact]
    public void An_out_of_service_defect_derives_a_fail()
    {
        var result = VehicleInspection.DeriveResult([Defect(InspectionDefectSeverity.OutOfService)]);

        Assert.Equal(InspectionResult.Fail, result);
    }

    [Fact]
    public void Create_stamps_the_derived_result()
    {
        var inspection = VehicleInspection.Create(
            TestVehicles.TenantId,
            "U-04",
            InspectionType.PreTrip,
            "J. Spence",
            InspectionSource.DriverApp,
            enteredBy: null,
            "TR-4818",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            118_204,
            [new InspectionChecklistItem { Group = "Exterior & Mechanical", Item = "Tires", Passed = false }],
            [Defect(InspectionDefectSeverity.OutOfService)]);

        Assert.Equal(InspectionResult.Fail, inspection.Result);
        Assert.Equal(TestVehicles.TenantId, inspection.TenantId);
    }
}
