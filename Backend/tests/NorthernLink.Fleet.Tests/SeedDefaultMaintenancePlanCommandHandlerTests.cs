using NorthernLink.Fleet.Application.Maintenance.Plans.SeedDefaults;
using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Fleet.Domain.Vehicles;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// The seed command must be safe to run any number of times AND against concurrent runs:
/// one plan (reused by name, never recreated — including a plan that lands between the name
/// probe and the insert), at most one NL-01 assignment (never overriding an operator's),
/// and a missing or disposed vehicle still seeds the plan. Uses the real transcribed
/// <see cref="TransitSeverePlanSeed"/> data.
/// </summary>
public class SeedDefaultMaintenancePlanCommandHandlerTests
{
    private static (SeedDefaultMaintenancePlanCommandHandler Handler,
        InMemoryMaintenancePlanRepository Plans,
        InMemoryPlanAssignmentRepository Assignments,
        InMemoryVehicleRepository Vehicles) Setup()
    {
        var plans = new InMemoryMaintenancePlanRepository();
        var assignments = new InMemoryPlanAssignmentRepository();
        var vehicles = new InMemoryVehicleRepository();
        var handler = new SeedDefaultMaintenancePlanCommandHandler(plans, assignments, vehicles);
        return (handler, plans, assignments, vehicles);
    }

    private static SeedDefaultMaintenancePlanCommand Command() => new(TestVehicles.TenantId);

    [Fact]
    public async Task Seeds_the_plan_and_assigns_the_NL01_vehicle_matched_by_vin()
    {
        var (handler, plans, assignments, vehicles) = Setup();
        var vehicle = TestVehicles.Create(vin: TransitSeverePlanSeed.Vin, unitNumber: TransitSeverePlanSeed.UnitNumber);
        vehicles.Add(vehicle);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var plan = Assert.Single(plans.Plans);
        Assert.Equal(plan.Id, result.Value);
        Assert.Equal(TransitSeverePlanSeed.PlanName, plan.Name);
        Assert.Equal(250, plan.Items.Count);
        Assert.Equal(10, plan.Overhauls.Count);
        var assignment = Assert.Single(assignments.Assignments);
        Assert.Equal(vehicle.Id, assignment.VehicleId);
        Assert.Equal(plan.Id, assignment.PlanId);
    }

    [Fact]
    public async Task Falls_back_to_the_unit_number_when_no_vehicle_matches_the_vin()
    {
        var (handler, plans, assignments, vehicles) = Setup();
        // Different VIN, but registered as unit NL-01.
        var vehicle = TestVehicles.Create(unitNumber: TransitSeverePlanSeed.UnitNumber);
        vehicles.Add(vehicle);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var assignment = Assert.Single(assignments.Assignments);
        Assert.Equal(vehicle.Id, assignment.VehicleId);
        Assert.Equal(Assert.Single(plans.Plans).Id, assignment.PlanId);
    }

    [Fact]
    public async Task Running_twice_changes_nothing_and_returns_the_same_plan_id()
    {
        var (handler, plans, assignments, vehicles) = Setup();
        vehicles.Add(TestVehicles.Create(vin: TransitSeverePlanSeed.Vin, unitNumber: TransitSeverePlanSeed.UnitNumber));

        var first = await handler.Handle(Command(), CancellationToken.None);
        var second = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value, second.Value);
        Assert.Single(plans.Plans);
        Assert.Single(assignments.Assignments);
        // No churn beyond the first run: one plan save, one assignment save (via TryAdd).
        Assert.Equal(1, plans.SaveChangesCallCount);
        Assert.Equal(1, assignments.SaveChangesCallCount);
    }

    [Fact]
    public async Task An_existing_plan_of_that_name_is_reused_not_recreated_or_updated()
    {
        var (handler, plans, assignments, vehicles) = Setup();
        vehicles.Add(TestVehicles.Create(vin: TransitSeverePlanSeed.Vin, unitNumber: TransitSeverePlanSeed.UnitNumber));
        // An operator-tuned plan already carries the seed name (one item, one overhaul).
        var existing = TestMaintenancePlans.Create(name: TransitSeverePlanSeed.PlanName);
        plans.Plans.Add(existing);
        var updatedAt = existing.UpdatedAtUtc;

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(existing.Id, result.Value);
        Assert.Single(plans.Plans);
        Assert.Equal(updatedAt, existing.UpdatedAtUtc);
        Assert.Equal(0, plans.SaveChangesCallCount);
        // The reused plan still gets assigned to the unassigned vehicle.
        Assert.Equal(existing.Id, Assert.Single(assignments.Assignments).PlanId);
    }

    [Fact]
    public async Task A_vehicle_already_on_any_plan_is_left_untouched()
    {
        var (handler, plans, assignments, vehicles) = Setup();
        var vehicle = TestVehicles.Create(vin: TransitSeverePlanSeed.Vin, unitNumber: TransitSeverePlanSeed.UnitNumber);
        vehicles.Add(vehicle);
        var otherPlan = TestMaintenancePlans.Create(name: "Operator's own program");
        plans.Plans.Add(otherPlan);
        assignments.Assignments.Add(
            PlanAssignment.Assign(TestVehicles.TenantId, vehicle.Id, otherPlan.Id).Value);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // The seed plan was still created and its id returned…
        Assert.Contains(plans.Plans, p => p.Id == result.Value && p.Name == TransitSeverePlanSeed.PlanName);
        // …but the existing assignment still points at the operator's plan.
        var assignment = Assert.Single(assignments.Assignments);
        Assert.Equal(otherPlan.Id, assignment.PlanId);
    }

    [Fact]
    public async Task A_missing_vehicle_still_seeds_the_plan()
    {
        var (handler, plans, assignments, _) = Setup();

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Assert.Single(plans.Plans).Id, result.Value);
        Assert.Empty(assignments.Assignments);
    }

    [Fact]
    public async Task A_disposed_vehicle_is_not_assigned_but_the_plan_still_seeds()
    {
        var (handler, plans, assignments, vehicles) = Setup();
        var vehicle = TestVehicles.Create(vin: TransitSeverePlanSeed.Vin, unitNumber: TransitSeverePlanSeed.UnitNumber);
        var retired = vehicle.ChangeStatus(VehicleStatus.Retired, "seed test");
        Assert.True(retired.IsSuccess);
        var disposed = vehicle.Dispose(DisposalMethod.Recycled);
        Assert.True(disposed.IsSuccess);
        vehicles.Add(vehicle);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(plans.Plans);
        Assert.Empty(assignments.Assignments);
    }

    [Fact]
    public async Task A_concurrent_seed_that_wins_the_name_race_still_yields_its_plan_id()
    {
        var (handler, plans, assignments, vehicles) = Setup();
        vehicles.Add(TestVehicles.Create(vin: TransitSeverePlanSeed.Vin, unitNumber: TransitSeverePlanSeed.UnitNumber));
        // Another request seeds the plan between this handler's name probe and its insert:
        // the unique (tenant_id, name) index fires, and the re-probe finds the winner's row.
        var winner = TestMaintenancePlans.Create(name: TransitSeverePlanSeed.PlanName);
        plans.ConcurrentWinnerOnNextTryAdd = winner;

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(winner.Id, result.Value);
        // Only the winner's plan exists — the loser's insert persisted nothing.
        Assert.Equal(winner.Id, Assert.Single(plans.Plans).Id);
        // The vehicle is still assigned, to the winner's plan.
        Assert.Equal(winner.Id, Assert.Single(assignments.Assignments).PlanId);
    }

    [Fact]
    public async Task A_concurrent_assignment_race_still_succeeds_with_the_plan_id()
    {
        var (handler, plans, assignments, vehicles) = Setup();
        vehicles.Add(TestVehicles.Create(vin: TransitSeverePlanSeed.Vin, unitNumber: TransitSeverePlanSeed.UnitNumber));
        // Another request assigns the vehicle between the handler's lookup and its save.
        assignments.FailNextTryAdd = true;

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Assert.Single(plans.Plans).Id, result.Value);
        Assert.Empty(assignments.Assignments);
    }
}
