using NorthernLink.Fleet.Application.Maintenance.Assignments.Assign;
using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Fleet.Domain.Vehicles;
using Xunit;

namespace NorthernLink.Fleet.Tests;

public class AssignMaintenancePlanCommandHandlerTests
{
    private static (AssignMaintenancePlanCommandHandler Handler,
        InMemoryPlanAssignmentRepository Assignments,
        InMemoryMaintenancePlanRepository Plans,
        InMemoryVehicleRepository Vehicles) Setup()
    {
        var assignments = new InMemoryPlanAssignmentRepository();
        var plans = new InMemoryMaintenancePlanRepository();
        var vehicles = new InMemoryVehicleRepository();
        return (new AssignMaintenancePlanCommandHandler(assignments, plans, vehicles), assignments, plans, vehicles);
    }

    [Fact]
    public async Task Assigns_a_plan_to_an_unassigned_vehicle()
    {
        var (handler, assignments, plans, vehicles) = Setup();
        var vehicle = TestVehicles.Create();
        vehicles.Add(vehicle);
        var plan = TestMaintenancePlans.Create();
        plans.Plans.Add(plan);

        var result = await handler.Handle(
            new AssignMaintenancePlanCommand(TestVehicles.TenantId, vehicle.Id, plan.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var assignment = Assert.Single(assignments.Assignments);
        Assert.Equal(vehicle.Id, assignment.VehicleId);
        Assert.Equal(plan.Id, assignment.PlanId);
        Assert.Equal(1, assignments.SaveChangesCallCount);
    }

    [Fact]
    public async Task Reassigns_when_the_vehicle_already_follows_a_plan()
    {
        var (handler, assignments, plans, vehicles) = Setup();
        var vehicle = TestVehicles.Create();
        vehicles.Add(vehicle);
        var oldPlan = TestMaintenancePlans.Create();
        var newPlan = TestMaintenancePlans.Create(name: "2019 International 3000 / severe");
        plans.Plans.Add(oldPlan);
        plans.Plans.Add(newPlan);
        var existing = PlanAssignment.Assign(TestVehicles.TenantId, vehicle.Id, oldPlan.Id).Value;
        assignments.Assignments.Add(existing);

        var result = await handler.Handle(
            new AssignMaintenancePlanCommand(TestVehicles.TenantId, vehicle.Id, newPlan.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var assignment = Assert.Single(assignments.Assignments);
        Assert.Equal(existing.Id, assignment.Id);
        Assert.Equal(newPlan.Id, assignment.PlanId);
        Assert.Equal(1, assignments.SaveChangesCallCount);
    }

    [Fact]
    public async Task Reassigning_the_same_plan_succeeds_without_restarting_the_clock()
    {
        var (handler, assignments, plans, vehicles) = Setup();
        var vehicle = TestVehicles.Create();
        vehicles.Add(vehicle);
        var plan = TestMaintenancePlans.Create();
        plans.Plans.Add(plan);
        var existing = PlanAssignment.Assign(TestVehicles.TenantId, vehicle.Id, plan.Id).Value;
        assignments.Assignments.Add(existing);
        var assignedAt = existing.AssignedAtUtc;

        var result = await handler.Handle(
            new AssignMaintenancePlanCommand(TestVehicles.TenantId, vehicle.Id, plan.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(assignedAt, Assert.Single(assignments.Assignments).AssignedAtUtc);
    }

    [Fact]
    public async Task A_concurrent_first_assignment_fails_with_conflict_not_a_500()
    {
        var (handler, assignments, plans, vehicles) = Setup();
        var vehicle = TestVehicles.Create();
        vehicles.Add(vehicle);
        var plan = TestMaintenancePlans.Create();
        plans.Plans.Add(plan);
        // Simulate the unique (tenant_id, vehicle_id) index firing between the handler's
        // lookup (no assignment yet) and its save.
        assignments.FailNextTryAdd = true;

        var result = await handler.Handle(
            new AssignMaintenancePlanCommand(TestVehicles.TenantId, vehicle.Id, plan.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.AssignmentConflict, result.Error);
        Assert.Empty(assignments.Assignments);
    }

    [Fact]
    public async Task An_unknown_vehicle_fails_with_not_found()
    {
        var (handler, assignments, plans, _) = Setup();
        var plan = TestMaintenancePlans.Create();
        plans.Plans.Add(plan);

        var result = await handler.Handle(
            new AssignMaintenancePlanCommand(TestVehicles.TenantId, Guid.NewGuid(), plan.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleErrors.NotFound, result.Error);
        Assert.Empty(assignments.Assignments);
        Assert.Equal(0, assignments.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_disposed_vehicle_fails_with_conflict()
    {
        var (handler, assignments, plans, vehicles) = Setup();
        var vehicle = TestVehicles.InStatus(VehicleStatus.Sold);
        vehicles.Add(vehicle);
        var plan = TestMaintenancePlans.Create();
        plans.Plans.Add(plan);

        var result = await handler.Handle(
            new AssignMaintenancePlanCommand(TestVehicles.TenantId, vehicle.Id, plan.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleErrors.Disposed, result.Error);
        Assert.Empty(assignments.Assignments);
    }

    [Fact]
    public async Task An_unknown_plan_fails_with_not_found()
    {
        var (handler, assignments, _, vehicles) = Setup();
        var vehicle = TestVehicles.Create();
        vehicles.Add(vehicle);

        var result = await handler.Handle(
            new AssignMaintenancePlanCommand(TestVehicles.TenantId, vehicle.Id, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.PlanNotFound, result.Error);
        Assert.Empty(assignments.Assignments);
    }
}
