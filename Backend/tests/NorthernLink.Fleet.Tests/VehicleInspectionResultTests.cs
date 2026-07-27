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
    public void Enter_stamps_the_derived_result()
    {
        var inspection = TestInspections.PreTrip(defects: [Defect(InspectionDefectSeverity.OutOfService)]);

        Assert.Equal(InspectionResult.Fail, inspection.Result);
        Assert.Equal(TestVehicles.TenantId, inspection.TenantId);
    }
}
