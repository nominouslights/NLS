using NorthernLink.Fleet.Application.Maintenance.Plans.Update;
using NorthernLink.Fleet.Domain.Maintenance;
using Xunit;

namespace NorthernLink.Fleet.Tests;

public class UpdateMaintenancePlanCommandHandlerTests
{
    private static UpdateMaintenancePlanCommand Command(
        Guid planId,
        string name = "2016 Ford Transit T-150 / severe") => new(
            TestVehicles.TenantId,
            planId,
            name,
            "2016 Ford Transit T-150",
            "normal",
            Notes: "updated",
            [TestMaintenancePlans.Item(), TestMaintenancePlans.Item("PM-E-002", component: "Oil level & condition")],
            []);

    [Fact]
    public async Task Updates_the_plan_wholesale()
    {
        var plans = new InMemoryMaintenancePlanRepository();
        var plan = TestMaintenancePlans.Create();
        plans.Plans.Add(plan);
        var handler = new UpdateMaintenancePlanCommandHandler(plans);

        var result = await handler.Handle(Command(plan.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("normal", plan.ServiceClass);
        Assert.Equal(2, plan.Items.Count);
        Assert.Empty(plan.Overhauls);
        Assert.Equal(1, plans.SaveChangesCallCount);
    }

    [Fact]
    public async Task An_unknown_plan_fails_with_not_found()
    {
        var plans = new InMemoryMaintenancePlanRepository();
        var handler = new UpdateMaintenancePlanCommandHandler(plans);

        var result = await handler.Handle(Command(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.PlanNotFound, result.Error);
    }

    [Fact]
    public async Task Renaming_onto_another_plans_name_fails_with_conflict_and_saves_nothing()
    {
        var plans = new InMemoryMaintenancePlanRepository();
        var plan = TestMaintenancePlans.Create();
        var other = TestMaintenancePlans.Create(name: "2019 International 3000 / severe");
        plans.Plans.Add(plan);
        plans.Plans.Add(other);
        var handler = new UpdateMaintenancePlanCommandHandler(plans);

        var result = await handler.Handle(
            Command(plan.Id, name: "2019 International 3000 / severe"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.NameTaken, result.Error);
        Assert.Equal("2016 Ford Transit T-150 / severe", plan.Name);
        Assert.Equal(0, plans.SaveChangesCallCount);
    }

    [Fact]
    public async Task Keeping_the_plans_own_name_succeeds()
    {
        var plans = new InMemoryMaintenancePlanRepository();
        var plan = TestMaintenancePlans.Create();
        plans.Plans.Add(plan);
        var handler = new UpdateMaintenancePlanCommandHandler(plans);

        var result = await handler.Handle(Command(plan.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, plans.SaveChangesCallCount);
    }
}
