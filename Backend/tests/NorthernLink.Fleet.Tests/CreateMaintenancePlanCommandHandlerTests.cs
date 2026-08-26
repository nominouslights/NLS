using NorthernLink.Fleet.Application.Maintenance.Plans.Create;
using NorthernLink.Fleet.Domain.Maintenance;
using Xunit;

namespace NorthernLink.Fleet.Tests;

public class CreateMaintenancePlanCommandHandlerTests
{
    private static CreateMaintenancePlanCommand Command(
        string name = "2016 Ford Transit T-150 / severe",
        IReadOnlyList<MaintenanceItem>? items = null) => new(
            TestVehicles.TenantId,
            name,
            "2016 Ford Transit T-150",
            "severe",
            Notes: null,
            items ?? [TestMaintenancePlans.Item()],
            [TestMaintenancePlans.Overhaul(relatedItemCodes: ["PM-E-001"])]);

    [Fact]
    public async Task Creates_a_plan_and_returns_its_id()
    {
        var plans = new InMemoryMaintenancePlanRepository();
        var handler = new CreateMaintenancePlanCommandHandler(plans);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(plans.Plans);
        Assert.Equal(result.Value, stored.Id);
        Assert.Equal("2016 Ford Transit T-150 / severe", stored.Name);
        Assert.Equal(1, plans.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_taken_name_fails_with_conflict_and_saves_nothing()
    {
        var plans = new InMemoryMaintenancePlanRepository();
        plans.Plans.Add(TestMaintenancePlans.Create());
        var handler = new CreateMaintenancePlanCommandHandler(plans);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.NameTaken, result.Error);
        Assert.Single(plans.Plans);
        Assert.Equal(0, plans.SaveChangesCallCount);
    }

    [Fact]
    public async Task The_duplicate_probe_trims_the_requested_name()
    {
        var plans = new InMemoryMaintenancePlanRepository();
        plans.Plans.Add(TestMaintenancePlans.Create());
        var handler = new CreateMaintenancePlanCommandHandler(plans);

        var result = await handler.Handle(
            Command(name: "  2016 Ford Transit T-150 / severe  "), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.NameTaken, result.Error);
    }

    [Fact]
    public async Task Domain_validation_failures_propagate_and_save_nothing()
    {
        var plans = new InMemoryMaintenancePlanRepository();
        var handler = new CreateMaintenancePlanCommandHandler(plans);
        var noInterval = TestMaintenancePlans.Item() with { IntervalKm = null, IntervalMonths = null };

        var result = await handler.Handle(Command(items: [noInterval]), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.ItemIntervalRequired, result.Error);
        Assert.Empty(plans.Plans);
        Assert.Equal(0, plans.SaveChangesCallCount);
    }
}
