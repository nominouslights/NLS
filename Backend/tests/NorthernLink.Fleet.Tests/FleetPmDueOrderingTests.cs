using NorthernLink.Fleet.Application.Maintenance;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// The fleet dashboard's urgency contract: overdue count first, then due-soon, then
/// not-yet-recorded (a never-serviced unit must not sink below fully compliant ones),
/// unit number as the stable tiebreak.
/// </summary>
public class FleetPmDueOrderingTests
{
    private static FleetVehiclePmDueResponse Row(
        string unit, int dueSoon = 0, int overdue = 0, int notYetRecorded = 0) => new(
        Guid.NewGuid(),
        unit,
        CurrentOdometerKm: 100_000,
        PlanId: Guid.NewGuid(),
        PlanName: "2016 Ford Transit T-150 / severe",
        dueSoon,
        overdue,
        notYetRecorded,
        DueEntries: []);

    [Fact]
    public void Orders_by_overdue_then_due_soon_then_not_yet_recorded_then_unit()
    {
        var compliant = Row("NL-01");
        var neverServiced = Row("NL-02", notYetRecorded: 260);
        var dueSoon = Row("NL-03", dueSoon: 2);
        var overdue = Row("NL-04", overdue: 1);

        var ordered = FleetPmDueResponse.OrderByUrgency([compliant, neverServiced, dueSoon, overdue]);

        Assert.Equal(
            new[] { "NL-04", "NL-03", "NL-02", "NL-01" },
            ordered.Select(r => r.UnitNumber));
    }

    [Fact]
    public void A_never_serviced_unit_outranks_a_compliant_one_but_not_real_findings()
    {
        var neverServiced = Row("NL-09", notYetRecorded: 260);
        var overdueAndFresh = Row("NL-10", overdue: 3, notYetRecorded: 0);
        var compliant = Row("NL-08");

        var ordered = FleetPmDueResponse.OrderByUrgency([compliant, neverServiced, overdueAndFresh]);

        Assert.Equal(
            new[] { "NL-10", "NL-09", "NL-08" },
            ordered.Select(r => r.UnitNumber));
    }

    [Fact]
    public void Equal_counts_fall_back_to_ordinal_unit_number()
    {
        var b = Row("NL-02", overdue: 1);
        var a = Row("NL-01", overdue: 1);

        var ordered = FleetPmDueResponse.OrderByUrgency([b, a]);

        Assert.Equal(new[] { "NL-01", "NL-02" }, ordered.Select(r => r.UnitNumber));
    }
}
